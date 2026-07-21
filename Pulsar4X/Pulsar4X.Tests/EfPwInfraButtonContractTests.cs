using System.Linq;
using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Components;
using Pulsar4X.Datablobs;      // ComponentInstancesDB
using Pulsar4X.Galaxy;         // PlanetRegionsDB, GroundHex, Region
using Pulsar4X.GroundCombat;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Operation Earthfall PW.2 — the CLIENT CROSS-LANE BUTTON CONTRACT gauge. CI cannot run the SDL client, so this
    /// fixture pins the EXACT engine surface the Force-Management / PlanetViewWindow battalion buttons draw against, so a
    /// breaking engine change reds CI instead of silently no-op'ing a button (the same role
    /// <c>EfC5TroopLiftOrderTests</c> plays for the embark/land buttons). It pins THREE things the buttons rely on:
    /// (1) the battalion RENAME setter (<see cref="GroundForces.RenameFormation"/>); (2) the two infra-order FACTORY
    /// field wires (<see cref="GroundOrder.DestroyInfra"/> / <see cref="GroundOrder.CaptureInfra"/> — the exact
    /// Type/TargetRegion/TargetQ/TargetR the buttons pass, with the hard-coded region-centre hex (0,0)); and (3) the
    /// QUEUE path the buttons issue on (<see cref="GroundForces.QueueFormationOrder"/>) resolving end-to-end — footprints
    /// land on the region-centre hex (0,0) (the buttons' hard-coded target), a queued DestroyInfra razes them, a queued
    /// CaptureInfra seizes the hex. The RESOLVE math itself is <c>EfGroundInfraCombatTests</c>' (G3); this pins the
    /// button-facing contract (rename + factory fields + the QUEUE path, not G3's <c>SetFormationOrder</c>).
    /// Engine-only → CI (`rest` shard).
    /// </summary>
    [TestFixture]
    public class EfPwInfraButtonContractTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[pw2] " + m);

        private const int Invader = 800088;

        private static GroundUnitDesign Sapper() => new GroundUnitDesign
        { UniqueID = "pw2-sapper", Name = "Sapper", UnitType = GroundUnitType.Infantry, Attack = 1000, Defense = 10, HitPoints = 500, Range = 1 };

        /// <summary>Place a real Bunker (a footprint building) on region 0's centre hex (0,0) via the same
        /// <see cref="GroundBuildings.LocateFootprintsOnHexes"/> path the live game uses. Mirrors
        /// <c>EfGroundInfraCombatTests.SetupBunker</c> but self-contained (no cross-fixture private-method reach).</summary>
        private static (ComponentInstance bunker, GroundHex centre, PlanetRegionsDB regions, Region region0) SetupBunker(TestScenario s)
        {
            var body = s.StartingBody;
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
            Assert.That(centre.InstallationIds, Does.Contain(bunker.ID),
                "precondition: footprints land on the region-CENTRE hex (0,0) — the coordinate the client buttons hard-code");
            return (bunker, centre, regions, region0);
        }

        // ───────────────────────── (1) RENAME button contract ─────────────────────────

        [Test]
        [Description("PW.2: the Rename button's setter — GroundForces.RenameFormation sets the name and returns true; a blank/whitespace name is rejected (returns false, keeps the old name), which is why the button's status reads 'rename ignored (blank name)'.")]
        public void Rename_SetsName_AndRejectsBlank()
        {
            var s = TestScenario.CreateWithColony();
            var f = GroundForces.CreateFormation(s.StartingBody, s.Faction.Id, "1st Bn");

            Assert.That(GroundForces.RenameFormation(f, "  Iron Guard  "), Is.True, "rename returns true on a real name");
            Assert.That(f.Name, Is.EqualTo("Iron Guard"), "the name is set (and trimmed)");

            Assert.That(GroundForces.RenameFormation(f, "   "), Is.False, "a blank/whitespace name is rejected");
            Assert.That(f.Name, Is.EqualTo("Iron Guard"), "the old name is kept on a rejected rename");
            Log("rename: sets+trims a real name, rejects blank keeping the old name");
        }

        // ───────────────────────── (2) infra-order FACTORY field wire ─────────────────────────

        [Test]
        [Description("PW.2: the two infra buttons pass GroundOrder.DestroyInfra/CaptureInfra(region, 0, 0). Pins the factory field wire (Type + TargetRegion + the hard-coded region-centre hex 0,0) so an arity/field drift reds CI, not a silent mis-targeted button.")]
        public void InfraOrderFactories_WireTheButtonFields()
        {
            var destroy = GroundOrder.DestroyInfra(2, 0, 0);
            Assert.That(destroy.Type, Is.EqualTo(GroundOrderType.DestroyInfrastructure));
            Assert.That(destroy.TargetRegion, Is.EqualTo(2));
            Assert.That(destroy.TargetQ, Is.EqualTo(0));
            Assert.That(destroy.TargetR, Is.EqualTo(0));

            var capture = GroundOrder.CaptureInfra(3, 0, 0);
            Assert.That(capture.Type, Is.EqualTo(GroundOrderType.CaptureInfrastructure));
            Assert.That(capture.TargetRegion, Is.EqualTo(3));
            Assert.That(capture.TargetQ, Is.EqualTo(0));
            Assert.That(capture.TargetR, Is.EqualTo(0));
            Log("factory: DestroyInfra/CaptureInfra wire Type + region + hex(0,0) exactly");
        }

        // ───────────────────────── (3) the QUEUE path the buttons issue on ─────────────────────────

        [Test]
        [Description("PW.2: the buttons issue via GroundForces.QueueFormationOrder (append), not SetFormationOrder. Pins that a QUEUED DestroyInfra(0,0,0) — the button's exact call — resolves through the ground tick and razes the region-centre-hex footprint, then pops. Clear empties the plan.")]
        public void QueuedDestroyInfra_FromTheButtonPath_RazesAndPops()
        {
            var s = TestScenario.CreateWithColony();
            var body = s.StartingBody;
            var (bunker, centre, regions, region0) = SetupBunker(s);
            region0.OwnerFactionID = Invader;

            // Field a co-located battalion (in region 0, on hex (0,0)) — what the client's leader-region targeting yields.
            var u = GroundForces.RaiseUnit(body, Sapper(), Invader, 0);
            u.HexQ = 0; u.HexR = 0;
            var f = GroundForces.CreateFormation(body, Invader, "Sappers");
            GroundForces.AssignUnit(f, u);

            // The EXACT button call: QUEUE the order (append), not SetFormationOrder.
            Assert.That(GroundForces.QueueFormationOrder(f, GroundOrder.DestroyInfra(0, 0, 0)), Is.True, "the queue accepts the order");
            Assert.That(f.Orders.Count, Is.EqualTo(1), "the button appended one order to the plan");

            var proc = new GroundForcesProcessor();
            for (int i = 0; i < 30 && region0.InstallationIds.Count > 0; i++) proc.ProcessEntity(body, 3600);

            Assert.That(region0.InstallationIds, Does.Not.Contain(bunker.ID), "the queued raze removed the footprint");
            Assert.That(centre.InstallationIds, Does.Not.Contain(bunker.ID), "and cleared it off the region-centre hex");
            Assert.That(f.Orders.Count, Is.EqualTo(0), "the queued destroy order popped once the hex was razed (never wedges)");

            GroundForces.QueueFormationOrder(f, GroundOrder.DestroyInfra(0, 0, 0));
            GroundForces.ClearFormationOrders(f);
            Assert.That(f.Orders.Count, Is.EqualTo(0), "Clear plan empties the queue (the 'Clear plan' button)");
            Log("queue path: appended DestroyInfra razed the footprint + popped; Clear empties the plan");
        }

        [Test]
        [Description("PW.2: a QUEUED CaptureInfra(0,0,0) — the Capture button's exact call — seizes the region-centre hex (flips GroundHex.OwnerFactionID to the battalion's faction) and pops. The button-path twin of G3's SetFormationOrder capture test.")]
        public void QueuedCaptureInfra_FromTheButtonPath_SeizesTheHex()
        {
            var s = TestScenario.CreateWithColony();
            var body = s.StartingBody;
            int defender = s.Faction.Id;
            var (bunker, centre, regions, region0) = SetupBunker(s);
            region0.OwnerFactionID = defender;
            Assert.That(centre.OwnerFactionID, Is.Not.EqualTo(Invader), "precondition: the hex is not yet the invader's");

            var u = GroundForces.RaiseUnit(body, Sapper(), Invader, 0);
            u.HexQ = 0; u.HexR = 0;
            var f = GroundForces.CreateFormation(body, Invader, "Sappers");
            GroundForces.AssignUnit(f, u);

            Assert.That(GroundForces.QueueFormationOrder(f, GroundOrder.CaptureInfra(0, 0, 0)), Is.True);
            new GroundForcesProcessor().ProcessEntity(body, 3600);

            Assert.That(centre.OwnerFactionID, Is.EqualTo(Invader), "the queued capture flipped the hex owner to the battalion's faction");
            Assert.That(f.Orders.Count, Is.EqualTo(0), "the queued capture order popped (instant v1)");
            Log($"queue path: appended CaptureInfra seized the hex for {Invader}; order popped");
        }
    }
}
