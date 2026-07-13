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
    /// Site Engine SE-2c — the posting-danger incident (docs/SITE-ENGINE-DESIGN.md §5). A leader seated in a berth at a
    /// DANGEROUS site can be lost; the site's Hook sets the risk, the berth's Survivability buys it down. Proves the
    /// danger math (SiteHazard), that the base Benign anomaly never rolls (byte-identical), and the grave rung — a
    /// certain incident vacates the berth and destroys the commander. Uses the seeded system RNG.
    /// </summary>
    [TestFixture]
    public class SitePostingDangerTests
    {
        private const int Day = 86400;

        private static Entity SpawnWorkerAt(TestScenario s, Vector3 spot)
        {
            var design = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns.Values.First();
            var ship = ShipFactory.CreateShip(design, s.Faction, s.StartingBody, "SE-2c Worker");
            ship.GetDataBlob<PositionDB>().AbsolutePosition = spot; // a clean spot → unambiguous on-site worker
            return ship;
        }

        private static Entity MakeLeader(TestScenario s)
        {
            var cdb = new CommanderDB("Dr. Ada", 1, CommanderTypes.Scientist);
            return CommanderFactory.Create(s.StartingSystem, s.Faction.Id, cdb);
        }

        private static CommandBerth SeatBerth(Entity host, Entity leader, int grade, int survivability)
        {
            var roster = new CommandBerthDB();
            roster.Berths.Add(new CommandBerth
            {
                Role = SiteRole.Science, Grade = grade, Survivability = survivability, ComponentName = "berth-1"
            });
            host.SetDataBlob(roster);
            BerthOps.SeatLeader(host, leader, SiteRole.Science);
            return roster.Berths[0];
        }

        [Test]
        [Description("SE-2c: the danger math — Hook base x (1 - Survivability/100) x days, Benign = 0, Survivability >= 100 immune, clamps to 1.")]
        public void IncidentChance_Math()
        {
            Assert.That(SiteHazard.IncidentChance(SiteHook.Benign, 0, 1), Is.EqualTo(0.0), "a benign site is never dangerous");
            Assert.That(SiteHazard.IncidentChance(SiteHook.Guardian, 0, 1), Is.EqualTo(0.02).Within(1e-9), "guardian base per day");
            Assert.That(SiteHazard.IncidentChance(SiteHook.Guardian, 50, 1), Is.EqualTo(0.01).Within(1e-9), "survivability halves it");
            Assert.That(SiteHazard.IncidentChance(SiteHook.Guardian, 100, 1), Is.EqualTo(0.0), "survivability 100 = immune");
            Assert.That(SiteHazard.IncidentChance(SiteHook.Guardian, 0, 3), Is.EqualTo(0.06).Within(1e-9), "scales with days");
            Assert.That(SiteHazard.IncidentChance(SiteHook.Guardian, 0, 1000), Is.EqualTo(1.0), "clamps to certainty");
        }

        [Test]
        [Description("SE-2c byte-identity: a Benign site (the base anomaly) never rolls an incident — its seated leader survives indefinitely.")]
        public void BenignSite_NeverLosesTheLeader()
        {
            var s = TestScenario.CreateWithColony();
            var spot = new Vector3(3.0e15, 0, 0);
            var ship = SpawnWorkerAt(s, spot);
            var leader = MakeLeader(s);
            var berth = SeatBerth(ship, leader, grade: 2, survivability: 0);

            // Benign hook + a huge understanding threshold so it stays Worked and keeps being processed.
            FieldSiteFactory.CreateAnomalySite(s.StartingSystem, spot, "Benign", hook: SiteHook.Benign,
                understandingToResolve: 1e9);

            var proc = new SiteWorkProcessor();
            for (int i = 0; i < 10; i++)
                proc.ProcessManager(s.StartingSystem, 100 * Day); // 1000 leader-days total, all safe

            Assert.That(berth.IsOccupied, Is.True, "a benign posting never harms the leader");
            Assert.That(leader.GetDataBlob<CommanderDB>().AssignedTo, Is.EqualTo(ship.Id), "the leader is still posted here");
        }

        [Test]
        [Description("SE-2c grave rung: a certain incident at a dangerous site vacates the berth and destroys the seated commander.")]
        public void DangerousSite_LosesTheLeader_VacatesTheBerth()
        {
            var s = TestScenario.CreateWithColony();
            var spot = new Vector3(3.5e15, 0, 0);
            var ship = SpawnWorkerAt(s, spot);
            var leader = MakeLeader(s);
            var berth = SeatBerth(ship, leader, grade: 2, survivability: 0); // no protection

            // Guardian hook + high understanding threshold so the site stays Worked (isolates the incident from resolve).
            FieldSiteFactory.CreateAnomalySite(s.StartingSystem, spot, "Guardian", hook: SiteHook.Guardian,
                understandingToResolve: 1e9);

            Assert.That(berth.IsOccupied, Is.True, "the leader starts seated");

            // 1000 days in one step → IncidentChance clamps to 1.0 → the incident is certain.
            new SiteWorkProcessor().ProcessManager(s.StartingSystem, 1000 * Day);

            Assert.That(berth.IsOccupied, Is.False, "the incident lost the leader — the berth is vacated (the grave rung)");
            Assert.That(berth.CommanderID, Is.EqualTo(-1));
            Assert.That(leader.GetDataBlob<CommanderDB>().AssignedTo, Is.EqualTo(-1), "the lost leader's posting is cleared");
        }
    }
}
