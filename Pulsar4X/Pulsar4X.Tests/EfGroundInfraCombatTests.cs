using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Components;
using Pulsar4X.Datablobs;      // ComponentInstancesDB
using Pulsar4X.Galaxy;         // PlanetRegionsDB, GroundHex
using Pulsar4X.GroundCombat;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Operation Earthfall G3 — INFRASTRUCTURE COMBAT (destroy / capture). A formation can now RAZE a footprint building
    /// on a hex it can reach (DestroyInfrastructure — staged, through the real component-removal path) and SEIZE a hex
    /// (CaptureInfrastructure — flips <see cref="GroundHex.OwnerFactionID"/>, so its buildings stop fortifying the
    /// defender — the first consumer that makes per-hex capture MATTER). Engine-only → CI (`rest` shard). Byte-identical
    /// until one of the two new orders is issued (nothing reads the appended enum members / the hex owner otherwise).
    /// </summary>
    [TestFixture]
    public class EfGroundInfraCombatTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[infra] " + m);

        private const int Invader = 800077;

        private static GroundUnitDesign Sapper() => new GroundUnitDesign
        { UniqueID = "efi-sapper", Name = "Sapper", UnitType = GroundUnitType.Infantry, Attack = 1000, Defense = 10, HitPoints = 500, Range = 1 };

        /// <summary>Place a real Bunker (a footprint building carrying a <see cref="GroundDefenseAtb"/>) on region 0's
        /// centre hex (0,0), hosted in the start colony — mirrors <c>GroundForcesTests.WarMap_FootprintBuilding_*</c>.</summary>
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
            GroundBuildings.LocateFootprintsOnHexes(s.Colony);   // drops the bunker onto the centre hex
            Assert.That(centre.InstallationIds, Does.Contain(bunker.ID), "precondition: the bunker sits on the war-map hex");
            return (bunker, centre, regions, region0);
        }

        /// <summary>Field one unit for <paramref name="factionId"/> in region 0 at the centre hex (0,0), in a formation
        /// holding <paramref name="order"/>. Co-located with the footprint so it's in range.</summary>
        private static GroundFormation FormationWithOrder(TestScenario s, int factionId, GroundOrder order)
        {
            var body = s.StartingBody;
            var u = GroundForces.RaiseUnit(body, Sapper(), factionId, 0);
            u.HexQ = 0; u.HexR = 0;   // stand on the target hex → in range of (0,0)
            var f = GroundForces.CreateFormation(body, factionId, "Sappers");
            GroundForces.AssignUnit(f, u);
            GroundForces.SetFormationOrder(f, order);
            return f;
        }

        // ───────────────────────── DESTROY ─────────────────────────

        [Test]
        [Description("G3: a DestroyInfrastructure order razes the footprint building on the target hex through the REAL removal path — gone from the hex, the region installation list, AND the colony's component store (a real loss). The order pops when the hex is razed.")]
        public void Destroy_RazesTheBuilding_ThroughTheRealRemovalPath()
        {
            var s = TestScenario.CreateWithColony();
            var body = s.StartingBody;
            var (bunker, centre, regions, region0) = SetupBunker(s);
            region0.OwnerFactionID = Invader;   // the invader holds the ground it's demolishing on

            var f = FormationWithOrder(s, Invader, GroundOrder.DestroyInfra(0, 0, 0));

            var proc = new GroundForcesProcessor();
            for (int i = 0; i < 30 && region0.InstallationIds.Count > 0; i++) proc.ProcessEntity(body, 3600);

            Assert.That(region0.InstallationIds, Does.Not.Contain(bunker.ID), "the bunker is razed from the region");
            Assert.That(centre.InstallationIds, Does.Not.Contain(bunker.ID), "and gone from the war-map hex");
            Assert.That(s.Colony.GetDataBlob<ComponentInstancesDB>().AllComponents.Values.Any(c => c.ID == bunker.ID), Is.False,
                "and gone from the colony's component store — a real loss, not just off the map");
            Assert.That(f.Orders.Count, Is.EqualTo(0), "the destroy order popped once the hex was razed (never wedges)");
            Log("destroy: bunker razed through the real removal path; order popped");
        }

        [Test]
        [Description("G3: a DestroyInfrastructure order from a formation NOT in reach (a unit standing in a different region) does not fire — the building is untouched and the order pops cleanly (never wedges the queue).")]
        public void Destroy_OutOfRange_DoesNotFire_AndPopsCleanly()
        {
            var s = TestScenario.CreateWithColony();
            var body = s.StartingBody;
            var (bunker, centre, regions, region0) = SetupBunker(s);
            Assert.That(regions.Regions.Count, Is.GreaterThanOrEqualTo(2));

            // The sapper stands in region 1, but the order targets region 0's hex → out of reach.
            var u = GroundForces.RaiseUnit(body, Sapper(), Invader, 1);
            var f = GroundForces.CreateFormation(body, Invader, "Far Sappers");
            GroundForces.AssignUnit(f, u);
            GroundForces.SetFormationOrder(f, GroundOrder.DestroyInfra(0, 0, 0));

            new GroundForcesProcessor().ProcessEntity(body, 3600);

            Assert.That(region0.InstallationIds, Does.Contain(bunker.ID), "out of reach → the building is untouched");
            Assert.That(centre.InstallationIds, Does.Contain(bunker.ID));
            Assert.That(f.Orders.Count, Is.EqualTo(0), "the un-fireable order still pops cleanly (never wedges)");
            Log("destroy out-of-range: building intact, order popped cleanly");
        }

        // ───────────────────────── CAPTURE + fortification consumer ─────────────────────────

        [Test]
        [Description("G3: a CaptureInfrastructure order flips the target hex's owner, and the captured bunker STOPS fortifying the defender — the first consumer that makes per-hex capture matter (fortification is no longer inert to hex ownership).")]
        public void Capture_FlipsHexOwner_AndStopsFortifyingTheDefender()
        {
            var s = TestScenario.CreateWithColony();
            var body = s.StartingBody;
            int defender = s.Faction.Id;
            var (bunker, centre, regions, region0) = SetupBunker(s);
            region0.OwnerFactionID = defender;   // the defender holds its own fortified region

            var resolveBefore = GroundFortification.BuildResolver(body);
            double fortBefore = GroundFortification.DefenseMult(region0, regions.Regions, defender, resolveBefore);
            Assert.That(fortBefore, Is.GreaterThan(1.0), "precondition: the bunker fortifies the defender's region");
            Assert.That(centre.OwnerFactionID, Is.Not.EqualTo(Invader), "precondition: the hex is not yet the invader's");

            var f = FormationWithOrder(s, Invader, GroundOrder.CaptureInfra(0, 0, 0));
            new GroundForcesProcessor().ProcessEntity(body, 3600);   // instant capture

            Assert.That(centre.OwnerFactionID, Is.EqualTo(Invader), "the capture order flipped the hex owner to the invader");
            double fortAfter = GroundFortification.DefenseMult(region0, regions.Regions, defender, GroundFortification.BuildResolver(body));
            Assert.That(fortAfter, Is.LessThan(fortBefore), "the captured bunker no longer fortifies the defender");
            Assert.That(fortAfter, Is.EqualTo(1.0).Within(1e-9), "with its only fort captured, the defender's region is un-fortified");
            Assert.That(f.Orders.Count, Is.EqualTo(0), "the capture order popped (instant v1)");
            Log($"capture: hex seized by {Invader}; defender fortification {fortBefore:0.00}x → {fortAfter:0.00}x");
        }
    }
}
