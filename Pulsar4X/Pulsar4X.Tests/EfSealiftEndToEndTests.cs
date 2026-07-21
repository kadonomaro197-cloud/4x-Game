using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Pulsar4X.Colonies;      // ColonyInfoDB (colony -> planet body)
using Pulsar4X.Energy;        // EnergyGenAbilityDB (stored energy for the charge check)
using Pulsar4X.Engine;        // Game, GameFactory, NewGameSettings, DevTestStartFactory, Entity, StarSystem
using Pulsar4X.Factions;      // FactionInfoDB, FactionState, ConquerResolver, StrategicObjective(DB), MilitaryTarget
using Pulsar4X.Fleets;        // FleetDB (clear UMF's strike fleet so Rung 2 fires)
using Pulsar4X.GroundCombat;  // GroundTransport, GroundCarryClass, GroundTransportDB
using Pulsar4X.Industry;      // IndustryAbilityDB, IndustryJob (find + complete the queued trooper)
using Pulsar4X.Modding;       // ModDataStore, ModLoader
using Pulsar4X.Movement;      // WarpAbilityDB, PositionDB
using Pulsar4X.Ships;         // ShipDesign, ShipInfoDB, LaunchComplexDB, LaunchComplexProcessor
using Pulsar4X.Storage;       // CargoStorageDB (fuel check)

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Operation Earthfall P4.4 — the END-TO-END SEALIFT gauge (findings/A4-sealift.md, fix seam 5 — the MISSING CI test).
    ///
    /// Plain English: the AI's invasion needs a troop transport, and the chain to get one is BUILD it at a colony → LAUNCH
    /// it off the pad into orbit → LOAD a garrison unit aboard → SAIL it at the enemy world. In the developer's real game
    /// NONE of that finished: 4 redundant builds strangled Mars industry (P4.1), a finished hull sat on a fuel-less pad
    /// (P4.3), and a built hull booted with a dead reactor so it couldn't warp (P4.2). All three are fixed — but the CI
    /// test that would PROVE the whole chain works was missing: <see cref="ConquerResolverTests"/> HAND-PLACES a fully
    /// staged, pre-loaded, pre-charged transport (its <c>PlaceLoadedTransportAt</c> helper), bypassing every link that
    /// died (industry, the launch complex, the fuel gate, the reactor charge, the LOAD rung).
    ///
    /// This fixture drives the chain through the REAL paths on the DevTest UMF's Mars (its only launch-complex colony):
    ///   1. QUEUE via the resolver — <see cref="ConquerResolver"/> Rung 2 emits "BuildTransport"; Execute queues a real
    ///      <c>IndustryJob</c> (+ sub-jobs) on a Mars shipyard line (no hand-placed job).
    ///   2. COMPLETE the queued trooper -> LAUNCH — the build is finished through its REAL production completion entry
    ///      point, <see cref="ShipDesign.OnConstructionComplete"/> (the exact call <c>IndustryTools</c> makes at job
    ///      completion, <c>IndustryTools.cs:230</c>). For a LAUNCH-COMPLEX colony (Mars) that routes the finished hull into
    ///      the colony's <see cref="LaunchComplexDB.LaunchQueue"/> — NOT straight to orbit. We DELIBERATELY do not drive
    ///      the material SUPPLY grind (mining -> refining): the per-day <c>IndustryProcessor</c> loop the first cut used
    ///      never fed the sub-components' inputs (battery/reactor/NTR), so the trooper parked at <c>MissingResources</c>
    ///      forever and the launch death-zone was never reached (the 2026-07-20 CI red). A4 confirms materials DO flow
    ///      under the real full clock — that is not the sealift bug — so this gauge completes the build directly
    ///      (Tests/CLAUDE.md gotcha #7: "gauging a BUILD through the industry queue is flaky — drive OnConstructionComplete
    ///      directly") and drives the REAL LAUNCH: <see cref="LaunchComplexProcessor"/> assigns the queued hull to a free
    ///      pad (the MaxTonnage gate), then <c>TryLaunchShip</c> deducts lift fuel (the P4.3 fuel), <c>CreateShip</c>s it in
    ///      Mars orbit, and <c>ProvisionBuiltShip</c> charges the reactor + fills the tanks (the P4.2 charge+fuel) — until
    ///      the transport is an OWNED IN-SYSTEM ENTITY. Processors driven directly (not the master clock) so no combat
    ///      fine-stepping can hang it; <c>[Timeout]</c> turns a genuine stall into a fast FAILURE.
    ///   3. LOAD — the resolver's Rung 1.5 finds the built transport (<see cref="ConquerResolver.FindOwnedTransport"/>) and
    ///      emits "LoadInvasion"; Execute loads a Mars garrison unit aboard through the real order path (an InstantOrder
    ///      that runs synchronously via <c>Game.OrderHandler.HandleOrder</c>, like the LAND order in ConquerResolverTests).
    ///   4. SAIL — with the transport now loaded, charged and warp-capable, the resolver's Rung 1.3 emits "SailTransport".
    ///
    /// The transport is NEVER hand-built — it is born by the REAL production completion + launch chain that A4 found dead.
    /// <see cref="ShipDesign.OnConstructionComplete"/> is a genuine production entry point (the one the industry processor
    /// calls), NOT hand-building a staged <c>ShipInfoDB</c>+<c>PositionDB</c>+<c>GroundTransportDB</c>. Byte-identity: this
    /// slice adds ONE test file + a notes-file section; it flips no engine flag (it pins the P4.2 NPC-charge policy to its
    /// DEFAULT only so a sibling test's flag leak can't perturb the charge assertion), touches no production code, and only
    /// READS + advances the DevTest via public/InternalsVisibleTo entry points.
    /// </summary>
    [TestFixture]
    public class EfSealiftEndToEndTests
    {
        private const string ScenarioDir = "Data/basemod/ScenarioFiles";

        // FLAGGED balance value: a generous cap on the number of LAUNCH passes driven before giving up. This is a TEST
        // BOUND, not a sim value — a single LaunchComplexProcessor pass both assigns the queued hull to a free pad AND
        // launches it, so the transport is in orbit on pass 1; the loop just re-tries defensively. If it isn't an owned
        // in-system entity by here, the LaunchQueue -> pad -> fuel -> CreateShip chain is genuinely broken (a true finding)
        // and the diagnostics dump below says where. The [Timeout] is the real backstop.
        private const int MaxLaunchDays = 30;   // FLAGGED balance value (test bound)

        private static Game NewDevTestGame()
        {
            var modDataStore = new ModDataStore();
            new ModLoader().LoadModManifest("Data/basemod/modInfo.json", modDataStore);
            return GameFactory.CreateGame(modDataStore, new NewGameSettings
            {
                MaxSystems = 1,
                CreatePlayerFaction = false,   // DevTest authors its own factions from JSON
                DefaultSolStart = true,
                MasterSeed = 12345,
                EleStart = true
            });
        }

        [Test, Timeout(300000)]
        [Description("Drives BUILD -> LAUNCH -> LOAD -> SAIL through the REAL production completion + launch + resolver "
                   + "paths on the DevTest UMF's Mars: the resolver queues a troop transport, the queued trooper is "
                   + "completed through ShipDesign.OnConstructionComplete (routing the hull onto Mars's LaunchQueue), the "
                   + "real LaunchComplexProcessor launches it into orbit (fuelled + charged per P4.3/P4.2 — never "
                   + "hand-placed), the resolver's LOAD rung finds it and loads a garrison unit, and the SAIL rung can "
                   + "emit. The end-to-end sealift gauge the CI never had (findings/A4-sealift.md seam 5).")]
        public void Sealift_BuildLaunchLoadSail_ThroughTheRealPaths_OnDevTestMars()
        {
            // Pin the P4.2 NPC-charge policy to its default (ON) so a sibling test in the shard that flips it (and
            // restores in a finally) can't perturb this test's "the built hull boots charged" assertion. Restored below.
            bool npcChargeWas = ShipDesign.ChargeBuiltNpcShips;
            var sb = new StringBuilder();
            sb.AppendLine("OPERATION EARTHFALL P4.4 — end-to-end sealift gauge (findings/A4-sealift.md)");
            try
            {
                ShipDesign.ChargeBuiltNpcShips = true;

                var game = NewDevTestGame();
                var (player, startingSystemId) = DevTestStartFactory.CreateDevTest(
                    game, ScenarioDir, new List<string> { "uef-devtest.json", "umf.json", "kithrin.json" });
                Assert.That(player, Is.Not.Null, "DevTest returned no player faction.");

                var sol = game.Systems.FirstOrDefault(s => s.ID == startingSystemId);
                Assert.That(sol, Is.Not.Null, "DevTest starting system '" + startingSystemId + "' not found in game.Systems.");

                // UMF = the multi-world militarist NPC (>= 4 colonies) — the aggressor that fields Mars's shipyards +
                // launch complex (the only launch-complex host in the sandbox).
                var umf = game.Factions.Values.FirstOrDefault(f =>
                    f != null && f.IsValid && f.HasDataBlob<FactionInfoDB>()
                    && f.GetDataBlob<FactionInfoDB>().IsNPC && f.GetDataBlob<FactionInfoDB>().Colonies.Count >= 4);
                Assert.That(umf, Is.Not.Null, "no multi-world NPC (UMF) in the DevTest sandbox.");
                var umfInfo = umf.GetDataBlob<FactionInfoDB>();

                // Mars = UMF's ONLY launch-complex colony — the hull built here must LAUNCH off a pad (the chain A4 found
                // dead), not spawn straight into orbit via the direct-CreateShip PARK path a non-launch colony uses.
                var mars = umfInfo.Colonies.FirstOrDefault(c => c != null && c.IsValid && c.HasDataBlob<LaunchComplexDB>());
                Assert.That(mars, Is.Not.Null,
                    "UMF has no launch-complex colony — the sealift LAUNCH chain can't be exercised (expected Mars).");
                Assert.That(mars.TryGetDataBlob<ColonyInfoDB>(out var marsColonyInfo) && marsColonyInfo.PlanetEntity != null,
                    Is.True, "Mars colony has no planet body.");
                var marsBody = marsColonyInfo.PlanetEntity;

                // Precondition: UMF is at war with UEF (opening relations), so MilitaryTarget scores a real enemy world
                // (Earth) and the resolver's war rungs run.
                Assert.That(MilitaryTarget.BestEnemyTarget(umf).IsValid, Is.True,
                    "UMF holds no valid war target — the ConquerResolver's sealift rungs won't fire (expected the opening "
                    + "war with UEF, whose Earth is the target).");

                // Remove UMF's ready strike fleet (the 3-warship "Mars Home Guard") so Rung 1 STRIKE doesn't preempt
                // Rung 2 BUILD — this ISOLATES the sealift chain (the legitimate state where the warfleet has already
                // sailed/been lost and the AI must build its invasion carrier). Destroy() only TAGS entities for removal;
                // because we drive processors directly (no master-clock subpulse runs), flush immediately with
                // RemoveTaggedEntitys so the query stops seeing them (else the destroyed fleet's blob still counts as a
                // ready strike fleet). Only UMF's fleets are cleared; the orphaned warships carry no troop bay, so they
                // never masquerade as the transport below.
                foreach (var fleet in sol.GetAllEntitiesWithDataBlob<FleetDB>().Where(f => f.FactionOwnerID == umf.Id).ToList())
                    fleet.Destroy();
                sol.RemoveTaggedEntitys();

                // ── STEP 1: QUEUE via the resolver (the REAL Rung-2 BUILD path — no hand-placed job) ──────────────────
                var buildAction = new ConquerResolver().Resolve(
                    FactionState.Snapshot(umf), new StrategicObjectiveDB { Objective = StrategicObjective.Conquer });
                Assert.That(buildAction.Kind, Is.EqualTo("BuildTransport"),
                    "with a war target, no owned transport, none already queued, and the strike fleet cleared, the "
                    + "resolver's Rung 2 should build the invasion carrier. Got Kind='" + buildAction.Kind
                    + "' (" + buildAction.Detail + ").");
                buildAction.Execute();   // queues a real IndustryJob (+ AutoAddSubJobs) on a Mars shipyard line
                Assert.That(ConquerResolver.FactionHasTransportQueued(FactionState.Snapshot(umf)), Is.True,
                    "after Execute a troop-transport job should be in production on Mars.");
                sb.AppendLine("STEP 1 ok: resolver Rung 2 queued a troop transport on Mars.");

                // ── STEP 2a: COMPLETE the queued trooper through its REAL completion entry point ──────────────────────
                // Find the actual queued transport SHIP job + its production line (the one buildAction.Execute added),
                // then complete it the way IndustryTools does at build-complete: designInfo.OnConstructionComplete(...)
                // (IndustryTools.cs:230). For a launch-complex colony this ADDS the finished hull to Mars's LaunchQueue
                // (ShipDesign.cs:137-145) and removes the job from its line — the exact production handoff, NOT a
                // hand-built entity. We complete it directly (rather than grinding the industry queue) because the SUPPLY
                // chain (mining -> refining -> the trooper's battery/reactor/NTR sub-components) is not driven by a bare
                // per-day IndustryProcessor loop, so the job would sit at MissingResources forever (the CI stall A4 says
                // is NOT the sealift bug — materials flow under the real full clock; the death-zone is the launch handoff).
                var (transportLineId, transportJob) = FindQueuedTransportJob(mars, umfInfo);
                Assert.That(transportJob, Is.Not.Null,
                    "STEP 1 queued a transport but no troop-transport SHIP job was found on a Mars production line to "
                    + "complete (only sub-component jobs?).");
                var transportDesign = (ShipDesign)umfInfo.IndustryDesigns[transportJob.ItemGuid];
                mars.TryGetDataBlob<CargoStorageDB>(out var marsStockpile);   // unused by the launch-complex branch, passed for fidelity
                transportDesign.OnConstructionComplete(mars, marsStockpile, transportLineId, transportJob, transportDesign);
                Assert.That(mars.TryGetDataBlob<LaunchComplexDB>(out var marsLaunch) && marsLaunch.LaunchQueue.Count > 0,
                    Is.True, "after OnConstructionComplete the finished trooper should be on Mars's LaunchQueue (the "
                    + "launch-complex production handoff, ShipDesign.cs:137-145).");
                sb.AppendLine("STEP 2a ok: queued trooper completed via ShipDesign.OnConstructionComplete -> on Mars's LaunchQueue.");

                // ── STEP 2b: drive the REAL LAUNCH until the transport is an OWNED IN-SYSTEM ENTITY ───────────────────
                // Drive Mars's LaunchComplexProcessor via its real ProcessEntity entry point (bounded). Each pass:
                // AssignQueueToPads assigns the queued hull to a free pad (the MaxTonnage gate), then ProcessPadLaunches
                // -> TryLaunchShip deducts the lift fuel (the P4.3 fuel), CreateShips the hull in Mars orbit, and
                // ProvisionBuiltShip charges the reactor + fills the tanks (the P4.2 charge+fuel). Bypasses the master
                // clock entirely, so no combat fine-stepping and no war-scene sim slows or hangs it.
                var launch = new LaunchComplexProcessor(); launch.Init(game);
                Entity transport = null;
                int launchDay = -1;
                for (int day = 1; day <= MaxLaunchDays && transport == null; day++)
                {
                    launch.ProcessEntity(mars, 86400);     // pad assign (MaxTonnage) + launch (fuel gate + charge)
                    transport = FindUmfTroopTransport(sol, umf.Id);
                    if (transport != null) launchDay = day;
                }

                if (transport == null)
                {
                    DumpLaunchState(sb, mars);
                    TestContext.Progress.WriteLine(sb.ToString());
                    Assert.Fail("the completed trooper never LAUNCHED into orbit within " + MaxLaunchDays
                        + " launch passes. The LaunchQueue -> pad -> fuel -> CreateShip chain is still broken — see the "
                        + "diagnostics above (launch queue / pads / fuel).");
                }
                sb.AppendLine("STEP 2b ok: transport LAUNCHED into orbit on launch pass " + launchDay
                    + " (born by the real completion + launch chain, NOT hand-placed).");

                // The launched hull sits in Mars orbit (positioned at the Mars body by TryLaunchShip), warp-capable.
                Assert.That(GroundTransport.ShipIsAtBody(transport, marsBody), Is.True,
                    "the launched transport should be positioned at the Mars body (TryLaunchShip put it in orbit there).");
                Assert.That(transport.TryGetDataBlob<WarpAbilityDB>(out var warp) && warp.MaxSpeed > 0, Is.True,
                    "the built transport has no working warp drive (MaxSpeed > 0) — it could never sail an invasion.");

                // ── CHARGED per P4.2: the built hull boots with a charged reactor + full tanks (NPC provisioning ON) ────
                double stored = StoredEnergy(transport), bubble = BubbleCost(transport), fuel = CargoMass(transport);
                sb.AppendLine("charged/fuelled: stored=" + stored.ToString("0") + " / bubble=" + bubble.ToString("0")
                    + " / cargo(fuel)=" + fuel.ToString("0"));
                Assert.That(stored, Is.GreaterThanOrEqualTo(bubble),
                    "the built transport booted with " + stored.ToString("0") + " stored energy — below its "
                    + bubble.ToString("0") + " warp-bubble cost, so it can't spin a bubble to sail (P4.2 charge regression).");
                Assert.That(fuel, Is.GreaterThan(0),
                    "the built transport carries no fuel — FillFuelTanks did not run on the launch path (P4.2 regression).");

                // ── STEP 3: LOAD — the resolver's Rung 1.5 finds the transport and loads a garrison unit ──────────────
                // The LOAD rung's own finder sees the built transport (this is "the LOAD rung finds it").
                Assert.That(ConquerResolver.FindOwnedTransport(FactionState.Snapshot(umf)), Is.EqualTo(transport),
                    "ConquerResolver.FindOwnedTransport (the LOAD rung's finder) did not return the built transport.");

                var loadAction = new ConquerResolver().Resolve(
                    FactionState.Snapshot(umf), new StrategicObjectiveDB { Objective = StrategicObjective.Conquer });
                Assert.That(loadAction.Kind, Is.EqualTo("LoadInvasion"),
                    "with a built transport at Mars and a garrison to load, the resolver's Rung 1.5 should LOAD the "
                    + "invasion. Got Kind='" + loadAction.Kind + "' (" + loadAction.Detail + ").");
                loadAction.Execute();   // InstantOrder -> runs synchronously via Game.OrderHandler.HandleOrder -> TryLoadUnit
                Assert.That(transport.TryGetDataBlob<GroundTransportDB>(out var carried) && carried.LoadedUnits.Count > 0,
                    Is.True, "after LoadInvasion the transport should be carrying a loaded ground unit (the load ran).");
                sb.AppendLine("STEP 3 ok: resolver Rung 1.5 loaded '" + carried.LoadedUnits[0].Name
                    + "' aboard the transport through the real order path.");

                // ── STEP 4: SAIL — with a loaded, charged, warp-capable transport, the SAIL rung can emit ──────────────
                var sailAction = new ConquerResolver().Resolve(
                    FactionState.Snapshot(umf), new StrategicObjectiveDB { Objective = StrategicObjective.Conquer });
                Assert.That(sailAction.Kind, Is.EqualTo("SailTransport"),
                    "with a loaded, charged, warp-capable transport not yet at the target, the resolver's Rung 1.3 should "
                    + "SAIL it at the enemy world. Got Kind='" + sailAction.Kind + "' (" + sailAction.Detail + ").");
                sb.AppendLine("STEP 4 ok: resolver Rung 1.3 can emit SailTransport — the built hull is ready to sail the invasion.");
                sb.AppendLine("SEALIFT CHAIN GREEN: BUILD -> LAUNCH -> LOAD -> SAIL through the real paths on DevTest Mars.");
            }
            finally
            {
                ShipDesign.ChargeBuiltNpcShips = npcChargeWas;
                TestContext.Progress.WriteLine(sb.ToString());
            }
        }

        /// <summary>The QUEUED troop-transport SHIP job on Mars + the production line it sits on — the exact job
        /// <c>buildAction.Execute()</c> added (matched by the same predicate <see cref="ConquerResolver.FactionHasTransportQueued"/>
        /// uses: a job whose design resolves to an <see cref="ConquerResolver.IsTroopTransport"/> ship). Returns the SHIP
        /// job, not its component sub-jobs (batteries/reactors/NTR), so <see cref="ShipDesign.OnConstructionComplete"/> can
        /// complete it against its real line. (null, null) if none is queued.</summary>
        private static (string lineId, IndustryJob job) FindQueuedTransportJob(Entity mars, FactionInfoDB info)
        {
            if (!mars.TryGetDataBlob<IndustryAbilityDB>(out var industry)) return (null, null);
            foreach (var (lineId, line) in industry.ProductionLines)
                foreach (var job in line.Jobs)
                    if (job != null
                        && info.IndustryDesigns.TryGetValue(job.ItemGuid, out var design)
                        && design is ShipDesign ship && ConquerResolver.IsTroopTransport(ship))
                        return (lineId, job);
            return (null, null);
        }

        /// <summary>The faction's owned troop transport in <paramref name="system"/> — a ship with a Personnel troop bay
        /// (the exact predicate <see cref="ConquerResolver.FindOwnedTransport"/> uses). A cheap per-iteration scan of one
        /// system (Sol) — no full FactionState snapshot in the hot loop.</summary>
        private static Entity FindUmfTroopTransport(StarSystem system, int factionId)
        {
            foreach (var ship in system.GetAllEntitiesWithDataBlob<ShipInfoDB>())
                if (ship != null && ship.IsValid && ship.FactionOwnerID == factionId
                    && GroundTransport.BayCapacity(ship, GroundCarryClass.Personnel) > 0)
                    return ship;
            return null;
        }

        /// <summary>Stored electricity in the ship's warp-drive energy type — the number the WarpMoveCommand gate
        /// (EnergyStored >= BubbleCreationCost) reads. Mirrors <see cref="NpcFleetReadyToSailTests"/>.</summary>
        private static double StoredEnergy(Entity ship)
        {
            var warp = ship.GetDataBlob<WarpAbilityDB>();
            var power = ship.GetDataBlob<EnergyGenAbilityDB>();
            string eType = warp.EnergyType;
            return (eType != null && power.EnergyStored.TryGetValue(eType, out var es)) ? es : 0;
        }

        private static double BubbleCost(Entity ship) => ship.GetDataBlob<WarpAbilityDB>().BubbleCreationCost;

        private static double CargoMass(Entity ship)
            => ship.TryGetDataBlob<CargoStorageDB>(out var cargo) ? cargo.TotalStoredMass : 0;

        /// <summary>Visibility Gate: dump Mars's production-line job statuses + the launch complex queue/pads + fuel, so a
        /// completed trooper that never launches reds with a diagnosable readout instead of a bare timeout.</summary>
        private static void DumpLaunchState(StringBuilder sb, Entity mars)
        {
            sb.AppendLine("---- LAUNCH DID NOT COMPLETE — Mars industry + launch state ----");
            if (mars.TryGetDataBlob<IndustryAbilityDB>(out var ind))
            {
                foreach (var (lineId, line) in ind.ProductionLines)
                    foreach (var job in line.Jobs)
                        sb.AppendLine("  job line=" + lineId + " item=" + job.ItemGuid + " status=" + job.Status
                            + " pointsLeft=" + job.ProductionPointsLeft + " completed=" + job.NumberCompleted + "/" + job.NumberOrdered);
            }
            if (mars.TryGetDataBlob<LaunchComplexDB>(out var lc))
            {
                sb.AppendLine("  launchQueue=" + lc.LaunchQueue.Count + " pads=" + lc.Pads.Count);
                foreach (var (padId, pad) in lc.Pads)
                    sb.AppendLine("    pad " + padId + ": maxTonnage=" + pad.MaxTonnage + " shipDesign=" + (pad.ShipDesignId ?? "<free>")
                        + " shipMass=" + pad.ShipMass + " ready=" + pad.ReadyToLaunch);
            }
            if (mars.TryGetDataBlob<CargoStorageDB>(out var cargo))
                sb.AppendLine("  colony fuel-storage present=" + cargo.TypeStores.ContainsKey("fuel-storage"));
        }
    }
}
