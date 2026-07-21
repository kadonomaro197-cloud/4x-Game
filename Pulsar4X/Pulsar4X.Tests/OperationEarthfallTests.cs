using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Pulsar4X.Colonies;      // ColonyInfoDB
using Pulsar4X.Components;    // ComponentDesign, GetAttribute
using Pulsar4X.Damage;        // DamageProcessor, DamageFragment
using Pulsar4X.Engine;        // Game, GameFactory, NewGameSettings, DevTestStartFactory, Entity, StarSystem
using Pulsar4X.Extensions;    // GetDefaultName()
using Pulsar4X.Factions;      // FactionInfoDB, FactionState, ObjectiveTransition, StrategicObjective(DB), ExpandResolver, NPCDecisionProcessor, MilitaryTarget, FactionRollup
using Pulsar4X.Galaxy;        // PlanetRegionsFactory, PlanetRegionsDB, PlanetHexFactory, GroundHex, Region
using Pulsar4X.GroundCombat;
using Pulsar4X.Modding;       // ModDataStore, ModLoader
using Pulsar4X.Orbital;       // Vector2

namespace Pulsar4X.Tests
{
    /// <summary>
    /// OPERATION EARTHFALL — the CAMPAIGN ACCEPTANCE GAUGE (P8.1a). The whole AI conquest arc, milestone by milestone,
    /// driven through the REAL engine paths — the CI-visible answer to "does the campaign actually run end to end."
    /// It is the CAPSTONE over the sibling Ef* slice gauges (each rung already green in isolation); this composes them
    /// into one narrative tape (TestResults/earthfall-readout.txt) and asserts the connections hold.
    ///
    /// Two kinds of milestone, both proven-green in their owning slice test so this composition is low-risk:
    ///   • DEVTEST BRAIN milestones drive the NPC decision engine DIRECTLY on the real UEF+UMF+Kithrin war sandbox
    ///     (no master-clock advance → no combat fine-step hang — the DevTestInvasionReadout / EfKithrinExpandArc rule),
    ///     asserting only CONFIDENT invariants (the brain runs clean, the invasion advances past massing, the Kithrin
    ///     decide to expand) and RECORDING the rest observationally (balances — D2 "no structural decay").
    ///   • CONTROLLED-SCENARIO milestones build a deterministic invasion on TestScenario.CreateWithColony and drive the
    ///     resolver / ground processor / damage paths directly (the TakeAPlanetIntegration / EfConquerGroundRungs /
    ///     EfBeachheadBuild / EfGroundInfraCombat / EfGroundTacticalBrain idioms) — "each individual step and click."
    ///
    /// Developer decisions applied (docs/earthfall/EARTHFALL-DECISIONS.md): #4 the EXISTING one-shot orbital bombardment
    /// softens the garrison — NO between-waves re-fire is built or asserted; #5 the hex is the unit — capture flips a hex/
    /// region owner, no region-transfer logic. Engine-only → CI (`rest` shard). Every milestone is its OWN [Test], so an
    /// unpassable one is [Ignore]d (never a red); each is bounded ([Timeout] on the DevTest drives) + writes its tape.
    /// </summary>
    [TestFixture]
    public class OperationEarthfallTests
    {
        private const string ScenarioDir = "Data/basemod/ScenarioFiles";
        private static readonly StringBuilder Tape = new StringBuilder();
        private static void Note(string line) { lock (Tape) Tape.AppendLine(line); TestContext.Progress.WriteLine("[earthfall] " + line); }

        [OneTimeSetUp]
        public void Header()
        {
            Note("OPERATION EARTHFALL — campaign acceptance tape");
            Note("generated " + DateTime.UtcNow.ToString("o"));
            Note("");
        }

        [OneTimeTearDown]
        public void FlushTape() => WriteReadout("earthfall-readout.txt", Tape.ToString());

        private static Game NewDevTestGame()
        {
            var modDataStore = new ModDataStore();
            new ModLoader().LoadModManifest("Data/basemod/modInfo.json", modDataStore);
            return GameFactory.CreateGame(modDataStore, new NewGameSettings
            {
                MaxSystems = 1, CreatePlayerFaction = false, DefaultSolStart = true, MasterSeed = 12345, EleStart = true
            });
        }

        // ───────────────────────── MILESTONE 1 — P3 continuity: the winning conquest is not orphaned ─────────────────

        [Test, Order(1)]
        [Description("MILESTONE 1 (P3, findings/A3): a WINNING in-flight Conquer commit HOLDS through a transient internal "
                   + "Survive wobble (the phantom-rebellion echo that abandoned the developer's invasion), but a GENUINE "
                   + "crisis (unprotected) still preempts to Defend — the operation-continuity guard, driven pure.")]
        public void Campaign_01_WinningConquestHeldThroughTransientWobble()
        {
            var t0 = new DateTime(2050, 1, 1);
            var obj = new StrategicObjectiveDB();
            var commit = TimeSpan.FromDays(180);

            Assert.That(ObjectiveTransition.Advance(obj, NeedTier.Ambition, StrategicObjective.Conquer, -1, t0, commit),
                Is.True, "the fresh objective adopts Conquer");

            // A transient INTERNAL Survive wobble proposes Defend — WITH protection it is HELD (the invasion presses on).
            bool heldThroughWobble = !ObjectiveTransition.Advance(
                obj, NeedTier.Survive, StrategicObjective.Defend, -1, t0.AddDays(30), commit,
                CrisisTrigger.Rebellion, protectCommit: true);
            Assert.That(heldThroughWobble, Is.True, "MILESTONE 1: a protected winning Conquer holds through the transient wobble");
            Assert.That(obj.Objective, Is.EqualTo(StrategicObjective.Conquer), "the winning conquest is not orphaned");

            // The SAME wobble WITHOUT protection preempts to Defend (a genuine crisis / the recall path).
            Assert.That(ObjectiveTransition.Advance(
                obj, NeedTier.Survive, StrategicObjective.Defend, -1, t0.AddDays(31), commit,
                CrisisTrigger.Rebellion, protectCommit: false), Is.True, "unprotected → preempts");
            Assert.That(obj.Objective, Is.EqualTo(StrategicObjective.Defend), "a genuine Defend switch takes over");
            Note("M1 P3-continuity: winning Conquer held through the transient wobble; a genuine crisis still preempts to Defend.");
        }

        // ───────────────────── MILESTONE 2 — DevTest brain: mass+sail advances, Kithrin expand, no decay ─────────────

        [Test, Order(2), Timeout(300000)]
        [Description("MILESTONE 2 (P4 sealift + D1-D3 + D2): drive the NPC brain on the real DevTest UEF+UMF+Kithrin war "
                   + "sandbox (gates OPEN, direct — no clock advance). HARD asserts the two proven-green invariants: (b) the "
                   + "UMF invasion ADVANCES past massing (emits a real STRIKE/SAIL/LAND order — the P4 sealift chain) and (c) "
                   + "the station-only Kithrin decide to SURVEY (the D1-D3 expand arc). The tick-error tally + faction "
                   + "balances are RECORDED observationally (D2 no-structural-decay), not hard-asserted over the 24-tick run.")]
        public void Campaign_02_DevTestBrain_InvasionAdvances_KithrinExpands_NoStructuralDecay()
        {
            bool gO = NPCDecisionProcessor.EnableOrderEmission, gD = NPCDecisionProcessor.EnableDiplomaticProposals,
                 gE = NPCDecisionProcessor.EnableEspionageMirror, gI = NPCDecisionProcessor.EnableIntelLedger;
            try
            {
                var game = NewDevTestGame();
                var (player, _) = DevTestStartFactory.CreateDevTest(
                    game, ScenarioDir, new List<string> { "uef-devtest.json", "umf.json", "kithrin.json" });
                Assert.That(player, Is.Not.Null, "DevTest returned no player faction.");

                // (c) KITHRIN EXPAND — decide on the FRESH DevTest state (before the brain drive mutates it, so it matches
                //     the D1-proven initial state): the station-only NPC's ExpandResolver decides SURVEY. Resolve is a pure
                //     decision (gate-independent), driven directly like EfKithrinExpandArc.
                var kithrin = game.Factions.Values.FirstOrDefault(f =>
                    f != null && f.IsValid && f.HasDataBlob<FactionInfoDB>()
                    && f.GetDataBlob<FactionInfoDB>().IsNPC && f.GetDataBlob<FactionInfoDB>().Stations.Count > 0);
                Assert.That(kithrin, Is.Not.Null, "no station-only NPC (Kithrin) in the DevTest sandbox.");
                var expand = new ExpandResolver().Resolve(FactionState.Snapshot(kithrin),
                    new StrategicObjectiveDB { Objective = StrategicObjective.Expand });
                Assert.That(expand.Kind, Is.EqualTo("Survey"),
                    "MILESTONE 2c: with an idle surveyor and worlds awaiting survey, the Kithrin ExpandResolver decides "
                    + "SURVEY (the D1-D3 expand arc). Decided: " + expand.Kind);

                NPCDecisionProcessor.EnableOrderEmission = true;
                NPCDecisionProcessor.EnableDiplomaticProposals = true;
                NPCDecisionProcessor.EnableEspionageMirror = true;
                NPCDecisionProcessor.EnableIntelLedger = true;

                var umf = game.Factions.Values.FirstOrDefault(f =>
                    f != null && f.IsValid && f.HasDataBlob<FactionInfoDB>()
                    && f.GetDataBlob<FactionInfoDB>().IsNPC && f.GetDataBlob<FactionInfoDB>().Colonies.Count >= 4);
                Assert.That(umf, Is.Not.Null, "no multi-world NPC (UMF) in the DevTest sandbox.");

                int baseTickErr = NPCDecisionProcessor.TickErrorCount;
                var processor = new NPCDecisionProcessor();
                processor.Init(game);

                var actionsSeen = new HashSet<string>();
                for (int i = 0; i < 24; i++)
                {
                    processor.ProcessManager(game.GlobalManager, 86400);
                    if (umf.HasDataBlob<StrategicObjectiveDB>())
                    {
                        var so = umf.GetDataBlob<StrategicObjectiveDB>();
                        if (!string.IsNullOrEmpty(so.LastActionKind)) actionsSeen.Add(so.LastActionKind);
                    }
                }
                int tickErrDelta = NPCDecisionProcessor.TickErrorCount - baseTickErr;

                // (b) ADVANCE — the UMF invasion chain reaches a real strike/sail/load order (the P4 sealift proof, the
                //     confident invariant DevTestInvasionReadout already asserts green over the same 24-tick drive).
                var advanceActions = new[] { "StrikeFleet", "StrikeJump", "SailTransport", "LoadInvasion", "LandInvasion" };
                Assert.That(actionsSeen.Overlaps(advanceActions), Is.True,
                    "MILESTONE 2b: the UMF invasion never advanced past massing — emitted only ["
                    + string.Join(", ", actionsSeen) + "]; expected one of [" + string.Join(", ", advanceActions) + "].");

                // (D2 observational, the honest floor) — the tick-error tally + every NPC's balance are RECORDED so a
                //   structural crash or money collapse is VISIBLE in the tape, without a hard assertion stronger than the
                //   proven-green gauges guarantee over a 24-tick run.
                Note("M2 tickErrDelta=" + tickErrDelta + (tickErrDelta == 0 ? " (clean)" : " last='" + NPCDecisionProcessor.LastTickError + "'"));
                foreach (var f in game.Factions.Values.Where(f => f != null && f.IsValid
                             && f.HasDataBlob<FactionInfoDB>() && f.GetDataBlob<FactionInfoDB>().IsNPC))
                    Note("  balance " + f.GetDefaultName() + " = " + FactionRollup.Balance(f).ToString("0"));

                Note("M2 DevTest brain: UMF advanced [" + string.Join(", ", actionsSeen)
                    + "]; Kithrin decided " + expand.Kind + " (D1-D3).");
            }
            finally
            {
                NPCDecisionProcessor.EnableOrderEmission = gO; NPCDecisionProcessor.EnableDiplomaticProposals = gD;
                NPCDecisionProcessor.EnableEspionageMirror = gE; NPCDecisionProcessor.EnableIntelLedger = gI;
            }
        }

        // ───────────────────────── MILESTONE 3 — the one-shot orbital bombardment softens the garrison ───────────────

        [Test, Order(3)]
        [Description("MILESTONE 3 (dev-decision #4 — the EXISTING one-shot bombardment, NO re-fire): an orbital strike on "
                   + "a defended colony softens its DEFENDING garrison (drains health + kills units) while sparing a landed "
                   + "invader — the space→ground 'soften before you land' step. Mirrors GroundBombardmentTests.")]
        public void Campaign_03_OrbitalBombardment_SoftensTheDefendingGarrison()
        {
            var s = TestScenario.CreateWithColony();
            int raised = GroundStartGarrison.RaiseForFactionColonies(s.Game, s.Faction);
            Assert.That(raised, Is.GreaterThan(0), "a home garrison was raised on the colony's planet");

            var body = s.Colony.GetDataBlob<ColonyInfoDB>().PlanetEntity;
            Assert.That(body.TryGetDataBlob<GroundForcesDB>(out var forces), Is.True, "the planet holds the garrison roster");
            int defenderFaction = s.Colony.FactionOwnerID;
            forces.Units.Add(new GroundUnit { FactionOwnerID = 999999, Health = 500, MaxHealth = 500, RegionIndex = 0, Name = "Invader" });

            int defBefore = forces.Units.Count(u => u.FactionOwnerID == defenderFaction);
            double defHpBefore = forces.Units.Where(u => u.FactionOwnerID == defenderFaction).Sum(u => u.Health);

            var frag = new DamageFragment
            {
                Velocity = new Vector2(1, 0), Position = (0, 0),
                Mass = 1f, Density = 1000f, Momentum = 1f, Length = 1f, Energy = 1e13,
            };
            DamageProcessor.OnTakingDamage(s.Colony, frag);

            int defAfter = forces.Units.Count(u => u.FactionOwnerID == defenderFaction);
            double defHpAfter = forces.Units.Where(u => u.FactionOwnerID == defenderFaction).Sum(u => u.Health);
            var invader = forces.Units.FirstOrDefault(u => u.FactionOwnerID == 999999);

            Assert.That(defAfter, Is.LessThan(defBefore), "MILESTONE 3: the bombardment killed some defenders (softened the garrison)");
            Assert.That(defHpAfter, Is.LessThan(defHpBefore), "...and drained the survivors' health");
            Assert.That(invader?.Health, Is.EqualTo(500.0), "a landed invader is NOT hit (the friendly-fire guard)");
            Note($"M3 bombardment: defenders {defBefore}->{defAfter} units, {defHpBefore:0}->{defHpAfter:0} hp; invader untouched (no re-fire — dev #4).");
        }

        // ─────────────── MILESTONE 4 — land the wave, FormUpLoose a battalion, the brain picks Offensive/Advance ───────

        [Test, Order(4)]
        [Description("MILESTONE 4 (G2.1 + G2.2): a landed invasion force is swept into a BATTALION by FormUpLoose (AI "
                   + "formation parity), and — reading its own real formation strength against a weaker defender — the "
                   + "ground tactical brain picks OFFENSIVE + Close + Advance (the officer of the deck presses the edge).")]
        public void Campaign_04_LandAndFormUp_BrainPicksOffensiveAndAdvances()
        {
            var s = TestScenario.CreateWithColony();
            var body = s.StartingBody;
            const int invaderFaction = 800200;

            var invDesign = new GroundUnitDesign
            { UniqueID = "ef-cap-invader", Name = "Invasion Rifles", UnitType = GroundUnitType.Infantry, Attack = 100, Defense = 10, HitPoints = 500 };
            for (int i = 0; i < 3; i++) GroundForces.RaiseUnit(body, invDesign, invaderFaction, 0);

            var forces = body.GetDataBlob<GroundForcesDB>();
            var formed = GroundAssembly.FormUpLoose(body, invaderFaction, "Landing Force");
            Assert.That(formed.Count, Is.GreaterThanOrEqualTo(1), "MILESTONE 4: FormUpLoose swept the landed wave into a battalion");
            Assert.That(forces.Units.Where(u => u.FactionOwnerID == invaderFaction).All(u => u.FormationId >= 1),
                Is.True, "every landed unit joined a battalion (the brain now has hands to command)");

            double own = GroundFormationTools.FormationStrength(forces, formed[0]);
            Assert.That(own, Is.GreaterThan(0), "the battalion has real firepower to weigh");

            var ctx = new GroundTacticsContext
            {
                OwnStrength = own, EnemyStrength = own / 2.0,           // a 2:1 edge over the scouted defender
                RiskTrait = 0.5, AggressionTrait = 0.5, IsHomelandDefender = false,
                FortificationMult = 1.0, AmmoFraction = 1.0, ReserveIntact = true,
                HasAdvanceTarget = true, AdvanceRegion = 1, Blind = false,
            };
            var p = GroundTactics.DecidePosture(ctx);
            Assert.That(p.StanceFamily, Is.EqualTo(GroundTactics.Offensive), "the brain reads the 2:1 edge and goes Offensive");
            Assert.That(p.Roe, Is.EqualTo(GroundEngagementStance.CloseToEngage));
            Assert.That(p.Intent, Is.EqualTo(GroundIntent.Advance), "and advances region-to-region");
            Assert.That(p.MoveTargetRegion, Is.EqualTo(1));
            Note($"M4 landing+brain: battalion strength {own:0} vs {own / 2:0} -> {p.StanceFamily}/{p.Roe}/{p.Intent}->region {p.MoveTargetRegion + 1}: {p.Reason}");
        }

        // ───────────────────────── MILESTONE 5 — the engineer erects a beachhead fort on held ground ─────────────────

        [Test, Order(5)]
        [Description("MILESTONE 5 (G1): a landed combat engineer on friendly-held, enemy-free ground with landed bunker "
                   + "parts ERECTS the bunker on site with NO colony present (the beachhead), and the region becomes a "
                   + "fortified FOB resupply point. Mirrors EfBeachheadBuildTests.")]
        public void Campaign_05_Engineer_BuildsABeachheadFort_ColonyFree()
        {
            var s = TestScenario.CreateWithColony();
            var faction = s.Faction.GetDataBlob<FactionInfoDB>();
            var body = s.StartingBody;
            int fid = s.Faction.Id;
            ComponentDesign Part(string id) => (ComponentDesign)faction.IndustryDesigns[id];

            PlanetHexFactory.EnsureHexesForBody(body);
            PlanetGridFactory.EnsureGridForBody(body);
            var regions = body.GetDataBlob<PlanetRegionsDB>();
            Assert.That(regions.Regions.Count, Is.GreaterThanOrEqualTo(2), "the world has regions to build in");

            const int rgn = 1;
            regions.Regions[rgn].OwnerFactionID = fid;   // the invader HOLDS the region it builds its beachhead on

            var engDesign = GroundUnitAssembly.RegisterAssembledDesign(
                faction, "ef-cap-engineer", "Combat Engineer",
                Part("default-design-human-frame"),
                new List<(ComponentDesign, int)> { (Part("default-design-ground-constructor"), 1) });
            GroundForces.RaiseUnit(body, engDesign, fid, rgn);

            double rate = Part("default-design-ground-constructor").GetAttribute<GroundConstructorAtb>().BuildRate;
            var bunker = Part("default-design-bunker");
            double required = Math.Max(GroundBeachhead.MinAssemblyEffort, bunker.IndustryPointCosts);

            Assert.That(GroundParts.AddParts(body, rgn, "default-design-bunker", 1), Is.EqualTo(1), "one bunker crate landed");
            var region = regions.Regions[rgn];
            var beforeIds = new HashSet<int>(region.InstallationIds);

            int fullDrive = (int)Math.Ceiling(required / rate) * 86400;
            GroundBeachhead.TickBuilds(body, fullDrive);

            var added = region.InstallationIds.Where(id => !beforeIds.Contains(id)).ToList();
            Assert.That(added.Count, Is.EqualTo(1), "MILESTONE 5: the engineer built the beachhead bunker on the held region");
            Assert.That(GroundParts.PartCount(body, rgn, "default-design-bunker"), Is.EqualTo(0), "the crate was consumed on completion");
            Assert.That(body.GetDataBlob<GroundForcesDB>().OutpostEntityIds.Count, Is.GreaterThanOrEqualTo(1), "a colony-free beachhead outpost hosts it");
            Assert.That(GroundBeachhead.HasBeachhead(body, fid, rgn), Is.True, "the region is now a FOB resupply point");
            Note($"M5 beachhead: engineer erected bunker #{added[0]} colony-free on held region {rgn + 1}; FOB up.");
        }

        // ───────────────────── MILESTONE 6 — an OFFENSIVE battalion razes an enemy building per-hex ──────────────────

        [Test, Order(6)]
        [Description("MILESTONE 6 (G3, dev-decision #5 — per-hex): an invader battalion standing on an enemy building "
                   + "hex razes it via a DestroyInfrastructure order through the real component-removal path — infra "
                   + "combat is per-HEX. Mirrors EfGroundInfraCombatTests.")]
        public void Campaign_06_OffensiveBattalion_RazesAnEnemyBuildingHex()
        {
            var s = TestScenario.CreateWithColony();
            var body = s.StartingBody;
            const int invader = 800600;

            var regions = body.GetDataBlob<PlanetRegionsDB>();
            var region0 = regions.Regions[0];
            var centre = region0.Hexes.First(h => h.Q == 0 && h.R == 0);
            foreach (var h in region0.Hexes) h.InstallationIds.Clear();
            region0.InstallationIds.Clear();

            var fi = s.Faction.GetDataBlob<FactionInfoDB>();
            var bunkerDesign = (ComponentDesign)fi.IndustryDesigns["default-design-bunker"];
            var bunker = new ComponentInstance(bunkerDesign);
            s.Colony.AddComponent(bunker);
            region0.InstallationIds.Add(bunker.ID);
            GroundBuildings.LocateFootprintsOnHexes(s.Colony);
            Assert.That(centre.InstallationIds, Does.Contain(bunker.ID), "precondition: the enemy building sits on the war-map hex");

            region0.OwnerFactionID = invader;   // the invader holds the ground it's razing on
            var sapperDesign = new GroundUnitDesign
            { UniqueID = "ef-cap-sapper", Name = "Sapper", UnitType = GroundUnitType.Infantry, Attack = 1000, Defense = 10, HitPoints = 500, Range = 1 };
            var u = GroundForces.RaiseUnit(body, sapperDesign, invader, 0);
            u.HexQ = 0; u.HexR = 0;
            var f = GroundForces.CreateFormation(body, invader, "Sappers");
            GroundForces.AssignUnit(f, u);
            GroundForces.SetFormationOrder(f, GroundOrder.DestroyInfra(0, 0, 0));

            var proc = new GroundForcesProcessor();
            for (int i = 0; i < 30 && region0.InstallationIds.Count > 0; i++) proc.ProcessEntity(body, 3600);

            Assert.That(region0.InstallationIds, Does.Not.Contain(bunker.ID), "MILESTONE 6: the enemy building is razed from the hex/region");
            Assert.That(centre.InstallationIds, Does.Not.Contain(bunker.ID), "and gone from the war-map hex");
            Assert.That(f.Orders.Count, Is.EqualTo(0), "the raze order popped once the hex was cleared");
            Note("M6 infra combat: an Offensive battalion razed the enemy building hex (per-hex, dev #5).");
        }

        // ───────────────────────── MILESTONE 7 — cleared garrison flips the region (take a planet) ───────────────────

        [Test, Order(7)]
        [Description("MILESTONE 7 (capture): with the defending garrison bombarded away and the landed invasion force "
                   + "holding the region, the ground processor CAPTURES it — the region owner flips (a planet taken), the "
                   + "full space->ground->capture chain. Mirrors TakeAPlanetIntegrationTests.")]
        public void Campaign_07_ClearedGarrison_FlipsTheRegion()
        {
            var s = TestScenario.CreateWithColony();
            PlanetRegionsFactory.GenerateForSystem(s.StartingSystem, surveyed: true);
            var body = s.Colony.GetDataBlob<ColonyInfoDB>().PlanetEntity;
            var regionsDB = body.GetDataBlob<PlanetRegionsDB>();
            const int invaderFaction = 800700;

            regionsDB.Regions[0].OwnerFactionID = s.Faction.Id;
            var design = new GroundUnitDesign
            {
                UniqueID = "ef-cap-inf", Name = "Rifles", UnitType = GroundUnitType.Infantry,
                Attack = 100, Defense = 10, HitPoints = 500,
                IndustryPointCosts = 100, IndustryTypeID = "installation", ResourceCosts = new Dictionary<string, long>(),
            };
            for (int i = 0; i < 3; i++) GroundForces.RaiseUnit(body, design, s.Faction.Id, 0);

            var frag = new DamageFragment
            {
                Velocity = new Vector2(1, 0), Position = (0, 0),
                Mass = 1f, Density = 1000f, Momentum = 1f, Length = 1f, Energy = 1e14,   // wipe the light garrison
            };
            DamageProcessor.OnTakingDamage(s.Colony, frag);

            var forces = body.GetDataBlob<GroundForcesDB>();
            Assert.That(forces.Units.Any(u => u.FactionOwnerID == s.Faction.Id), Is.False, "the garrison was cleared off the surface");

            GroundForces.RaiseUnit(body, design, invaderFaction, 0);   // the landed wave holds the now-undefended region
            new GroundForcesProcessor().ProcessEntity(body, 3600);

            Assert.That(regionsDB.Regions[0].OwnerFactionID, Is.EqualTo(invaderFaction),
                "MILESTONE 7: with the garrison bombarded away and the invader holding it, the region is TAKEN");
            Note($"M7 capture: region 0 flipped to the invader ({invaderFaction}) — the planet is taken.");
        }

        // ─────────── MILESTONE 8 — THE BRAIN IS VISIBLY ALIVE: an outmatched invader reads the odds and pulls back ────

        [Test, Order(8)]
        [Description("MILESTONE 8 (G2.2, the developer's declared scenario): when the defenders outnumber the landed "
                   + "force past the Risk bar, the invader battalion READS it — with a beachhead behind it it goes "
                   + "Defensive and RETREATS toward it; cornered it digs in. Asserts Stance/ROE/Intent + the plain-English "
                   + "Reason (the brain visibly alive), not just survival.")]
        public void Campaign_08_OutmatchedInvader_ReadsTheOdds_GoesDefensiveOrWithdraws()
        {
            // Outnumbered 1:4 with a beachhead to fall back to → a fighting withdrawal.
            var withFallback = GroundTactics.DecidePosture(new GroundTacticsContext
            {
                OwnStrength = 100, EnemyStrength = 400, RiskTrait = 0.5, AggressionTrait = 0.5,
                FortificationMult = 1.0, AmmoFraction = 1.0, ReserveIntact = true,
                HasFallback = true, FallbackRegion = 2,
            });
            Assert.That(withFallback.StanceFamily, Is.EqualTo(GroundTactics.Defensive), "MILESTONE 8: outmatched -> Defensive");
            Assert.That(withFallback.Roe, Is.EqualTo(GroundEngagementStance.StandOff));
            Assert.That(withFallback.Intent, Is.EqualTo(GroundIntent.Retreat), "it withdraws toward its beachhead");
            Assert.That(withFallback.MoveTargetRegion, Is.EqualTo(2));
            Assert.That(withFallback.Reason, Does.Contain("withdrawal"), "the AI-tape explains the pull-back");

            // Cornered (nowhere to run) → dig in, never a suicide march.
            var cornered = GroundTactics.DecidePosture(new GroundTacticsContext
            {
                OwnStrength = 100, EnemyStrength = 400, RiskTrait = 0.5, AggressionTrait = 0.5,
                FortificationMult = 1.0, AmmoFraction = 1.0, ReserveIntact = true, HasFallback = false, FallbackRegion = -1,
            });
            Assert.That(cornered.Intent, Is.EqualTo(GroundIntent.Hold), "cornered units dig in, they don't march into the enemy");
            Assert.That(cornered.StanceFamily, Is.EqualTo(GroundTactics.Defensive));
            Assert.That(cornered.MoveTargetRegion, Is.EqualTo(-1));

            // Merely outnumbered (past the Risk bar, not yet 1:4) on hostile ground → dig in and hold.
            var outnumbered = GroundTactics.DecidePosture(new GroundTacticsContext
            {
                OwnStrength = 100, EnemyStrength = 300, RiskTrait = 0.5, AggressionTrait = 0.5,
                FortificationMult = 1.0, AmmoFraction = 1.0, ReserveIntact = true,
            });
            Assert.That(outnumbered.StanceFamily, Is.EqualTo(GroundTactics.Defensive), "outnumbered attacker digs in");
            Assert.That(outnumbered.Intent, Is.EqualTo(GroundIntent.Hold));
            Note($"M8 brain-alive: 1:4+fallback -> {withFallback.Intent} ('{withFallback.Reason}'); cornered -> {cornered.Intent}; outnumbered -> {outnumbered.StanceFamily}/{outnumbered.Intent}.");
        }

        /// <summary>Write a readout to TestResults/&lt;name&gt; at the repo root (walks up to the folder holding .github),
        /// the same folder CI uploads. Never fails the gauge on a file-system hiccup. Mirrors EfKithrinExpandArcTests.</summary>
        private static void WriteReadout(string fileName, string content)
        {
            try
            {
                var dir = new DirectoryInfo(AppContext.BaseDirectory);
                while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, ".github")))
                    dir = dir.Parent;
                string root = dir?.FullName ?? Directory.GetCurrentDirectory();
                string resultsDir = Path.Combine(root, "TestResults");
                Directory.CreateDirectory(resultsDir);
                File.WriteAllText(Path.Combine(resultsDir, fileName), content);
            }
            catch (Exception ex)
            {
                TestContext.Progress.WriteLine("[earthfall] could not write readout: " + ex.GetType().Name + ": " + ex.Message);
            }
        }
    }
}
