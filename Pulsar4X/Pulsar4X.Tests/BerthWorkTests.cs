using System;
using System.Linq;
using NUnit.Framework;
using Pulsar4X.DataStructures;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Movement;
using Pulsar4X.Orbital;
using Pulsar4X.People;
using Pulsar4X.Ships;
using Pulsar4X.Sites;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Site Engine SE-2b — seating a leader in a Command Berth and having that manned berth work a field-site FASTER
    /// (docs/SITE-ENGINE-DESIGN.md §5). Proves the leader-in-the-loop: BerthOps.SeatLeader fills a berth + sets the
    /// leader's back-reference; SiteWorkProcessor scales the work rate by the manned berth's Grade + the leader's
    /// competence (+ Support). Additive / byte-identical — a worker with no manned matching berth still works at the
    /// SE-1b flat rate (multiplier 1.0), so every SE-1b/SE-1c gauge is unchanged.
    /// </summary>
    [TestFixture]
    public class BerthWorkTests
    {
        private static Entity SpawnWorker(TestScenario s, out Vector3 workerPos)
        {
            var design = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns.Values.First();
            var ship = ShipFactory.CreateShip(design, s.Faction, s.StartingBody, "SE-2b Worker");
            workerPos = ship.GetDataBlob<PositionDB>().AbsolutePosition;
            return ship;
        }

        private static Entity MakeLeader(TestScenario s, int experience)
        {
            var cdb = new CommanderDB("Dr. Ada", 1, CommanderTypes.Scientist);
            cdb.Experience = experience;
            return CommanderFactory.Create(s.StartingSystem, s.Faction.Id, cdb);
        }

        private static CommandBerthDB AttachRoster(Entity host, int grade, int support)
        {
            var roster = new CommandBerthDB();
            roster.Berths.Add(new CommandBerth
            {
                Role = SiteRole.Science, Grade = grade, Support = support, ComponentName = "berth-1"
            });
            host.SetDataBlob(roster);
            return roster;
        }

        [Test]
        [Description("SE-2b: SeatLeader fills the best empty matching berth and sets the leader's AssignedTo back-reference; VacateBerth frees both.")]
        public void SeatLeader_FillsBerth_AndSetsBackref_VacateFrees()
        {
            var s = TestScenario.CreateWithColony();
            var ship = SpawnWorker(s, out _);
            var roster = AttachRoster(ship, grade: 2, support: 0);
            var leader = MakeLeader(s, experience: 0);

            Assert.That(BerthOps.SeatLeader(ship, leader, SiteRole.Science), Is.True, "an empty science berth seats the leader");
            var berth = roster.Berths[0];
            Assert.That(berth.CommanderID, Is.EqualTo(leader.Id), "the berth records its occupant");
            Assert.That(berth.IsOccupied, Is.True);
            Assert.That(leader.GetDataBlob<CommanderDB>().AssignedTo, Is.EqualTo(ship.Id), "the leader's back-reference points at the host");

            Assert.That(BerthOps.SeatLeader(ship, MakeLeader(s, 0), SiteRole.Science), Is.False,
                "the only science berth is full → a second leader can't seat");

            Assert.That(BerthOps.VacateBerth(ship, leader.Id), Is.True, "vacating frees the berth");
            Assert.That(berth.IsOccupied, Is.False);
            Assert.That(leader.GetDataBlob<CommanderDB>().AssignedTo, Is.EqualTo(-1), "vacating clears the leader's back-reference");
        }

        [Test]
        [Description("SE-2b: a manned berth's multiplier is Grade x (1 + leaderSkill + Support/100); Grade 2, Exp 100 (skill 0.5) -> 3.0.")]
        public void BerthWorkMultiplier_ScalesWithGrade_Skill_Support()
        {
            var s = TestScenario.CreateWithColony();

            // Grade 2, no Support, leader skill 0.5 (Exp 100 / 200) → 2 * (1 + 0.5) = 3.0
            var ship = SpawnWorker(s, out _);
            AttachRoster(ship, grade: 2, support: 0);
            BerthOps.SeatLeader(ship, MakeLeader(s, experience: 100), SiteRole.Science);
            Assert.That(SiteWorkProcessor.BerthWorkMultiplier(ship, SiteRole.Science), Is.EqualTo(3.0).Within(1e-6));

            // Support also lifts it: Grade 1, Support 20, Exp 0 → 1 * (1 + 0 + 0.2) = 1.2
            var ship2 = SpawnWorker(s, out _);
            AttachRoster(ship2, grade: 1, support: 20);
            BerthOps.SeatLeader(ship2, MakeLeader(s, experience: 0), SiteRole.Science);
            Assert.That(SiteWorkProcessor.BerthWorkMultiplier(ship2, SiteRole.Science), Is.EqualTo(1.2).Within(1e-6));

            // A Role mismatch is not worked by this berth → flat 1.0.
            Assert.That(SiteWorkProcessor.BerthWorkMultiplier(ship, SiteRole.Tactical), Is.EqualTo(1.0).Within(1e-6));
        }

        [Test]
        [Description("SE-2b byte-identity: a worker with no berth, or an unmanned berth, works at the SE-1b flat rate (multiplier 1.0).")]
        public void NoBerthOrEmptyBerth_IsFlatRate()
        {
            var s = TestScenario.CreateWithColony();

            var bare = SpawnWorker(s, out _);
            Assert.That(SiteWorkProcessor.BerthWorkMultiplier(bare, SiteRole.Science), Is.EqualTo(1.0).Within(1e-6),
                "no roster → flat rate (SE-1b behaviour preserved)");

            var empty = SpawnWorker(s, out _);
            AttachRoster(empty, grade: 5, support: 50); // a high-grade berth, but nobody seated
            Assert.That(SiteWorkProcessor.BerthWorkMultiplier(empty, SiteRole.Science), Is.EqualTo(1.0).Within(1e-6),
                "an EMPTY berth gives no bonus — a leader does the work");
        }

        [Test]
        [Description("SE-2b end-to-end: a manned Grade-2 berth on the on-site worker banks 2x the flat progress in a day.")]
        public void MannedBerth_WorksSiteFaster_EndToEnd()
        {
            var s = TestScenario.CreateWithColony();
            var ship = SpawnWorker(s, out _);

            // Relocate the worker to a clean, empty spot so it is unambiguously the on-site worker (no start-fleet tie).
            var spot = new Vector3(2.5e15, 0, 0);
            ship.GetDataBlob<PositionDB>().AbsolutePosition = spot;

            AttachRoster(ship, grade: 2, support: 0);
            BerthOps.SeatLeader(ship, MakeLeader(s, experience: 0), SiteRole.Science); // skill 0 → multiplier = Grade = 2

            var site = FieldSiteFactory.CreateAnomalySite(s.StartingSystem, spot, "Berthed Anomaly");
            var siteDB = site.GetDataBlob<FieldSiteDB>();

            new SiteWorkProcessor().ProcessManager(s.StartingSystem, 86400); // one day

            Assert.That(siteDB.Progress, Is.EqualTo(SiteWorkProcessor.WorkPerDay * 2.0).Within(1e-6),
                "a Grade-2 manned berth banks 2x the flat WorkPerDay");
            Assert.That(siteDB.Understanding, Is.EqualTo(SiteWorkProcessor.UnderstandingPerDay * 2.0).Within(1e-6),
                "understanding scales by the same multiplier");
        }
    }
}
