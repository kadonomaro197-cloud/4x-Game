using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pulsar4X.Colonies;      // LegitimacyDB, ColonyInfoDB
using Pulsar4X.Components;    // ComponentDesign, GetAttribute
using Pulsar4X.Engine;        // Game, Entity
using Pulsar4X.Factions;      // FactionInfoDB, StrategicObjectiveDB, StrategicObjective, CrisisTrigger
using Pulsar4X.Galaxy;        // PlanetRegionsDB, PlanetHexFactory, PlanetGridFactory
using Pulsar4X.GroundCombat;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// OPERATION EARTHFALL — MID-CAMPAIGN SAVE/LOAD (P8.1c). Builds a realistic MID-INVASION snapshot (troops landed +
    /// formed into a battalion, a brain-set posture, a queued brain-ISSUED order, a placed beachhead building + landed
    /// surface parts, a captured hex, and the P3 crisis state — the rebellion-debounce counter + the objective's
    /// commit-reason), round-trips it through Game.Save → Game.Load (the SaveLoadDesignRoundTripTests / SaveLoadWithJobTests
    /// pattern — both proven to round-trip a CreateWithColony game green), and asserts the campaign state survives intact.
    /// Then it advances a tick with the tactical brain ON and asserts the campaign CONTINUES (the brain re-decides without
    /// error). Engine-only → CI (`rest` shard).
    ///
    /// The order-ISSUER marker (GroundOrderIssuer.Ai), the hex OwnerFactionID flip, the beachhead outpost + surface parts,
    /// and the P3 CommitTrigger/ContradictionCycles + LegitimacyDB.ConsecutiveCollapsingReads are all appended [JsonProperty]
    /// fields with deep-copy discipline — this is the CI sensor that a later rename/drop of any of them doesn't silently
    /// corrupt a mid-invasion save.
    /// </summary>
    [TestFixture]
    public class MidCampaignSaveLoadTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[midsave] " + m);

        private static GroundUnitDesign Infantry(string id) => new GroundUnitDesign
        { UniqueID = id, Name = "Legionnaire", UnitType = GroundUnitType.Infantry, Attack = 100, Defense = 10, HitPoints = 500 };

        [Test, Timeout(180000)]
        [Description("A mid-invasion snapshot (landed battalion, brain posture, an AI-issued queued order, a placed "
                   + "beachhead + surface parts, a captured hex, and the P3 crisis state) survives Game.Save -> Game.Load "
                   + "intact, and the campaign CONTINUES on the next tick (the brain re-decides without error).")]
        public void MidInvasionSnapshot_SurvivesSaveLoad_AndTheCampaignContinues()
        {
            var s = TestScenario.CreateWithColony();
            var faction = s.Faction.GetDataBlob<FactionInfoDB>();
            var body = s.StartingBody;
            int inv = s.Faction.Id;                 // the invading NPC (owns the base-mod parts the beachhead engineer needs)
            faction.IsNPC = true;                   // the brain only drives NPC factions
            ComponentDesign Part(string p) => (ComponentDesign)faction.IndustryDesigns[p];

            PlanetHexFactory.EnsureHexesForBody(body);
            PlanetGridFactory.EnsureGridForBody(body);
            var regions = body.GetDataBlob<PlanetRegionsDB>();
            Assert.That(regions.Regions.Count, Is.GreaterThanOrEqualTo(3), "the world has regions for the invasion state");

            // ── landed battalion in region 0, with a brain-set posture and an AI-issued queued order ──
            GroundForces.RaiseUnit(body, Infantry("ef-mid-a"), inv, 0);
            GroundForces.RaiseUnit(body, Infantry("ef-mid-b"), inv, 0);
            var legion = GroundForces.CreateFormation(body, inv, "1st Legion");
            var forces = body.GetDataBlob<GroundForcesDB>();
            foreach (var u in forces.Units.Where(u => u.FactionOwnerID == inv).ToList()) GroundForces.AssignUnit(legion, u);
            int membersBefore = GroundFormationTools.MemberCount(forces, legion);
            legion.StanceFamily = GroundTactics.Offensive;
            legion.TacticalReason = "odds favour the assault — pressing";
            legion.TacticalIntent = GroundIntent.Advance;
            var aiOrder = GroundOrder.MoveRegion(1);
            aiOrder.Issuer = GroundOrderIssuer.Ai;
            GroundForces.SetFormationOrder(legion, aiOrder);

            // ── a captured hex (region 0 centre) — the OwnerFactionID flip ──
            var centre = regions.Regions[0].Hexes.First(h => h.Q == 0 && h.R == 0);
            centre.OwnerFactionID = inv;

            // ── a beachhead building erected in region 1 (colony-free) + a landed surface-parts crate in region 2 ──
            regions.Regions[1].OwnerFactionID = inv;
            var engDesign = GroundUnitAssembly.RegisterAssembledDesign(
                faction, "ef-mid-engineer", "Combat Engineer",
                Part("default-design-human-frame"),
                new List<(ComponentDesign, int)> { (Part("default-design-ground-constructor"), 1) });
            GroundForces.RaiseUnit(body, engDesign, inv, 1);
            double rate = Part("default-design-ground-constructor").GetAttribute<GroundConstructorAtb>().BuildRate;
            var bunker = Part("default-design-bunker");
            double required = Math.Max(GroundBeachhead.MinAssemblyEffort, bunker.IndustryPointCosts);
            GroundParts.AddParts(body, 1, "default-design-bunker", 1);
            GroundBeachhead.TickBuilds(body, (int)Math.Ceiling(required / rate) * 86400);   // build the beachhead bunker
            Assert.That(forces.OutpostEntityIds.Count, Is.GreaterThanOrEqualTo(1), "precondition: a beachhead outpost was built");
            Assert.That(regions.Regions[1].InstallationIds.Count, Is.GreaterThanOrEqualTo(1), "precondition: the bunker is on the region");
            GroundParts.AddParts(body, 2, "default-design-bunker", 1);   // a landed crate that stays a SurfacePart
            Assert.That(forces.SurfaceParts.Count, Is.GreaterThanOrEqualTo(1), "precondition: a landed surface-parts crate exists");

            // ── P3 crisis state: the objective's commit-reason + the rebellion-debounce counter ──
            s.Faction.SetDataBlob(new StrategicObjectiveDB
            {
                Objective = StrategicObjective.Conquer,
                CommitTrigger = CrisisTrigger.Rebellion,
                ContradictionCycles = 1,
            });
            Assert.That(s.Colony.TryGetDataBlob<LegitimacyDB>(out var legit), Is.True, "the colony carries a LegitimacyDB");
            legit.ConsecutiveCollapsingReads = 1;   // a rebellion debounce in progress

            int bodyId = body.Id, colonyId = s.Colony.Id;
            string abbr = faction.Abbreviation;

            // ── SAVE → LOAD ──
            string json = null;
            Assert.DoesNotThrow(() => json = Game.Save(s.Game), "Game.Save threw on the mid-invasion snapshot.");
            Assert.That(json, Is.Not.Null.And.Not.Empty, "Game.Save produced no JSON.");
            Game reloaded = null;
            Assert.DoesNotThrow(() => reloaded = Game.Load(json), "Game.Load threw on the mid-invasion save.");
            Assert.That(reloaded, Is.Not.Null, "Game.Load returned null.");

            // ── locate the reloaded body / colony / faction ──
            var rBody = reloaded.Systems.SelectMany(sys => sys.GetAllEntitiesWithDataBlob<GroundForcesDB>())
                .FirstOrDefault(e => e.Id == bodyId);
            Assert.That(rBody, Is.Not.Null, "the invaded body round-tripped with its ground forces");
            var rForces = rBody.GetDataBlob<GroundForcesDB>();
            var rRegions = rBody.GetDataBlob<PlanetRegionsDB>();

            var rColony = reloaded.Systems.SelectMany(sys => sys.GetAllEntitiesWithDataBlob<ColonyInfoDB>())
                .FirstOrDefault(e => e.Id == colonyId);
            Assert.That(rColony, Is.Not.Null, "the colony round-tripped");
            var rFaction = reloaded.Factions.Values.FirstOrDefault(f =>
                f.HasDataBlob<FactionInfoDB>() && f.GetDataBlob<FactionInfoDB>().Abbreviation == abbr);
            Assert.That(rFaction, Is.Not.Null, "the invading faction round-tripped");

            // ── (1) formations + membership ──
            var rLegion = rForces.Formations.FirstOrDefault(f => f.Name == "1st Legion");
            Assert.That(rLegion, Is.Not.Null, "the battalion survived save/load");
            Assert.That(GroundFormationTools.MemberCount(rForces, rLegion), Is.EqualTo(membersBefore),
                "the battalion's membership survived");
            Assert.That(rLegion.StanceFamily, Is.EqualTo(GroundTactics.Offensive), "the brain-set stance survived");
            Assert.That(rLegion.TacticalIntent, Is.EqualTo(GroundIntent.Advance), "the brain-set intent survived");
            Assert.That(rLegion.TacticalReason, Is.EqualTo("odds favour the assault — pressing"), "the AI-tape reason survived");

            // ── (2) order queue INCLUDING the issuer marker ──
            Assert.That(rLegion.Orders.Count, Is.EqualTo(1), "the queued order survived");
            Assert.That(rLegion.Orders[0].Type, Is.EqualTo(GroundOrderType.MoveToRegion));
            Assert.That(rLegion.Orders[0].TargetRegion, Is.EqualTo(1));
            Assert.That(rLegion.Orders[0].Issuer, Is.EqualTo(GroundOrderIssuer.Ai),
                "the order-issuer marker (Ai) survived — the human-vs-brain ownership is preserved");

            // ── (3) hex OwnerFactionID flip ──
            var rCentre = rRegions.Regions[0].Hexes.First(h => h.Q == 0 && h.R == 0);
            Assert.That(rCentre.OwnerFactionID, Is.EqualTo(inv), "the captured hex's owner flip survived");

            // ── (4) beachhead building + surface parts ──
            Assert.That(rForces.OutpostEntityIds.Count, Is.GreaterThanOrEqualTo(1), "the beachhead outpost host survived");
            Assert.That(rRegions.Regions[1].InstallationIds.Count, Is.GreaterThanOrEqualTo(1), "the beachhead bunker on the region survived");
            Assert.That(rForces.SurfaceParts.Count, Is.GreaterThanOrEqualTo(1), "the landed surface-parts crate survived");

            // ── (5) P3 crisis state: commit-reason + rebellion-debounce counter ──
            Assert.That(rFaction.TryGetDataBlob<StrategicObjectiveDB>(out var rObj), Is.True, "the strategic objective survived");
            Assert.That(rObj.Objective, Is.EqualTo(StrategicObjective.Conquer), "the committed objective survived");
            Assert.That(rObj.CommitTrigger, Is.EqualTo(CrisisTrigger.Rebellion), "the commit-reason (CrisisTrigger) survived");
            Assert.That(rObj.ContradictionCycles, Is.EqualTo(1), "the contradiction-debounce counter survived");
            Assert.That(rColony.TryGetDataBlob<LegitimacyDB>(out var rLegit), Is.True, "the colony's LegitimacyDB survived");
            Assert.That(rLegit.ConsecutiveCollapsingReads, Is.EqualTo(1), "the rebellion-debounce counter survived");
            Log("mid-invasion snapshot round-tripped: formations, order+issuer, hex flip, beachhead+parts, P3 crisis state all intact.");

            // ── (6) THE CAMPAIGN CONTINUES — advance a tick with the tactical brain ON; it re-decides without error ──
            bool prevBrain = GroundForcesProcessor.EnableGroundTacticalAI;
            try
            {
                GroundForcesProcessor.EnableGroundTacticalAI = true;
                Assert.DoesNotThrow(() => new GroundForcesProcessor().ProcessEntity(rBody, 3600),
                    "the ground tick threw on the reloaded mid-invasion state (the campaign did NOT continue).");
                Assert.That(rBody.GetDataBlob<GroundForcesDB>().Formations.Any(f => f.Name == "1st Legion"), Is.True,
                    "the battalion is still present after the tick — the campaign continues");
                Log("campaign continues: the ground tick ran on the reloaded state with the brain on, no error.");
            }
            finally { GroundForcesProcessor.EnableGroundTacticalAI = prevBrain; }
        }

        [Test]
        [Ignore("TWOD battle-frame anchors round-trip is not exercised here: the 2D group-plane anchors "
              + "(FleetCombatStateDB / GroupPlane) are written only behind EnableGroupPlane (default OFF) during a "
              + "formed SPACE engagement, which this ground-focused mid-campaign snapshot does not stand up. The anchor "
              + "FIELDS' save-safety is structurally covered: FleetCombatStateDB's group-plane fields carry [JsonProperty] "
              + "and are deep-copied in its copy-ctor/Clone (verified by the audit's cross-cutting-invariants pass; the "
              + "engine-wide Save/Load round-trip rides SaveLoadDesignRoundTripTests). A dedicated mid-battle anchor "
              + "round-trip awaits a combined space+ground harness. See docs/earthfall/LANE-CORE-NOTES.md P8.1c.")]
        [Description("(TWOD, deferred) the 2D battle-frame anchors survive a mid-battle save/load — owned by the TWOD lane's anchor gauges.")]
        public void MidBattle_GroupPlaneAnchors_SurviveSaveLoad() { }
    }
}
