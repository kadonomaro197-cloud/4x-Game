using NUnit.Framework;
using Pulsar4X.Combat;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Fleets;
using Pulsar4X.Movement;
using Pulsar4X.Ships;
using System.Linq;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// OPERATION BLUEPRINT-TO-STEEL B4a (Intercept) — the ENGINE half of "order a fleet to intercept a detected enemy
    /// fleet." Intercept reuses the existing warp-to-a-moving-target order (`WarpFleetTowardsTargetOrder`), so the only
    /// NEW engine surface is (1) a FOG-AWARE target list — <see cref="CombatEngagement.DetectedHostileFleets"/> (the
    /// `OrderAttackNearestHostile` filter returning ALL detected hostiles) — and (2) resolving an enemy fleet to a
    /// positioned ship to warp toward (a `FleetDB` has no `PositionDB`) — <see cref="FleetTools.RepresentativeShip"/>.
    /// The client picker + the order dispatch ride these; the AI can call the same primitive (one verb, both seats).
    /// </summary>
    [TestFixture]
    public class InterceptTargetingTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[intercept] " + m);

        private static Entity MakeFleetWithShip(TestScenario s, Entity faction, string name)
        {
            var fleet = FleetFactory.Create(s.StartingSystem, faction.Id, name);
            var design = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns.Values.First(); // build under player faction (proven pattern)
            var ship = ShipFactory.CreateShip(design, s.Faction, s.StartingBody, name + " ship");
            ship.FactionOwnerID = faction.Id;
            s.Game.OrderHandler.HandleOrder(FleetOrder.AssignShip(faction.Id, fleet, ship));
            return fleet;
        }

        [Test]
        [Description("DetectedHostileFleets lists a different-faction fleet with ships (fog off → detected), and excludes " +
                     "a same-faction (friendly) fleet and the querying fleet itself — the fog-aware target list the " +
                     "Intercept picker + AI rung read.")]
        public void DetectedHostileFleets_ListsHostilesWithShips_ExcludesFriendlyAndSelf()
        {
            var s = TestScenario.CreateWithColony();
            var reds = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);

            var mine = MakeFleetWithShip(s, s.Faction, "Mine");
            var enemy = MakeFleetWithShip(s, reds, "Enemy");
            var friendly = MakeFleetWithShip(s, s.Faction, "Friendly");

            var targets = CombatEngagement.DetectedHostileFleets(mine);
            Log($"targets for 'Mine': {string.Join(", ", targets.Select(t => t.Id))}");
            Assert.That(targets, Does.Contain(enemy), "a different-faction fleet with ships is a hostile target (fog off → detected)");
            Assert.That(targets, Does.Not.Contain(friendly), "a same-faction fleet is not a hostile target");
            Assert.That(targets, Does.Not.Contain(mine), "the querying fleet is never its own target");

            // Symmetry: the enemy sees 'Mine' as a hostile target too (different non-neutral factions are mutually hostile).
            Assert.That(CombatEngagement.DetectedHostileFleets(enemy), Does.Contain(mine),
                        "hostility is mutual — the enemy fleet lists mine");
        }

        [Test]
        [Description("RepresentativeShip resolves the positioned ship a fleet-targeting order warps toward: a fleet with " +
                     "a ship yields a POSITIONED ship; an empty fleet yields null (no NRE).")]
        public void RepresentativeShip_ReturnsAPositionedShip_NullForEmpty()
        {
            var s = TestScenario.CreateWithColony();
            var fleet = MakeFleetWithShip(s, s.Faction, "Fleet");

            var rep = FleetTools.RepresentativeShip(fleet);
            Assert.That(rep, Is.Not.Null, "a fleet with a ship has a representative ship");
            Assert.That(rep.HasDataBlob<PositionDB>(), Is.True, "the representative ship is positioned — a valid warp target");
            Assert.That(FleetTools.AllShipsRecursive(fleet), Does.Contain(rep), "the representative is one of the fleet's own ships");

            var empty = FleetFactory.Create(s.StartingSystem, s.Faction.Id, "Empty");
            Assert.That(FleetTools.RepresentativeShip(empty), Is.Null, "an empty fleet has no representative ship (no throw)");
        }
    }
}
