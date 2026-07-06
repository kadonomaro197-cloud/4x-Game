using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pulsar4X.Colonies;
using Pulsar4X.Damage;
using Pulsar4X.Engine;
using Pulsar4X.Galaxy;
using Pulsar4X.GroundCombat;
using Pulsar4X.Orbital;   // Vector2

namespace Pulsar4X.Tests
{
    /// <summary>
    /// THE MVP CAPSTONE — "you can take a planet" as a CONNECTED chain, not just isolated pieces (Prime Directive:
    /// the real test is the cross-system stack). This exercises space combat → ground combat → capture end to end
    /// through the real engine paths: orbital bombardment (<see cref="DamageProcessor.OnTakingDamage"/> → the new
    /// garrison-softening path) clears a colony's defending garrison, then a landed invader is left holding the region,
    /// and the ground processor (<see cref="GroundForcesProcessor"/>) CAPTURES it.
    ///
    /// The complement — an invader that CANNOT take a full, un-bombarded garrison (3 defenders beat 1) — is
    /// <c>GroundForcesTests.RegionCombat_StrongerGarrisonWins_AndTakesTheRegion</c>. Together they show the orbital
    /// bombardment is precisely what turns an unwinnable ground assault into a capture: soften from orbit, then land.
    /// Engine-only → runs in CI.
    /// </summary>
    [TestFixture]
    public class TakeAPlanetIntegrationTests
    {
        private const int InvaderFaction = 900042;
        private static void Log(string m) => TestContext.Progress.WriteLine("[take-planet] " + m);

        private static GroundUnitDesign MakeInfantryDesign() => new GroundUnitDesign
        {
            UniqueID = "test-ground-infantry",
            Name = "Test Rifles",
            UnitType = GroundUnitType.Infantry,
            Attack = 100, Defense = 10, HitPoints = 500,
            IndustryPointCosts = 100, IndustryTypeID = "installation",
            ResourceCosts = new Dictionary<string, long>(),
        };

        [Test]
        [Description("End-to-end: orbital bombardment clears a colony's defending garrison off the surface, then a landed invader is captured-into the now-undefended region by the ground processor — space combat → ground combat → capture, connected through the real engine paths.")]
        public void Bombardment_ClearsGarrison_ThenTheInvaderTakesTheRegion()
        {
            var s = TestScenario.CreateWithColony();
            PlanetRegionsFactory.GenerateForSystem(s.StartingSystem, surveyed: true);
            var body = s.Colony.GetDataBlob<ColonyInfoDB>().PlanetEntity;
            var regionsDB = body.GetDataBlob<PlanetRegionsDB>();

            // The defender (the colony owner) holds region 0 with a 3-unit garrison.
            regionsDB.Regions[0].OwnerFactionID = s.Faction.Id;
            var design = MakeInfantryDesign();
            GroundForces.RaiseUnit(body, design, s.Faction.Id, 0);
            GroundForces.RaiseUnit(body, design, s.Faction.Id, 0);
            GroundForces.RaiseUnit(body, design, s.Faction.Id, 0);
            Assert.That(regionsDB.Regions[0].OwnerFactionID, Is.EqualTo(s.Faction.Id), "precondition: the defender holds region 0");

            // 1) BOMBARD from orbit — a heavy strike on the colony clears the garrison off the surface.
            var frag = new DamageFragment
            {
                Velocity = new Vector2(1, 0), Position = (0, 0),
                Mass = 1f, Density = 1000f, Momentum = 1f, Length = 1f,
                Energy = 1e14,   // enough to wipe the light garrison outright
            };
            DamageProcessor.OnTakingDamage(s.Colony, frag);

            var forces = body.GetDataBlob<GroundForcesDB>();
            Assert.That(forces.Units.Any(u => u.FactionOwnerID == s.Faction.Id), Is.False,
                "orbital bombardment cleared the defending garrison off the surface");

            // 2) LAND the invader into the now-undefended region.
            GroundForces.RaiseUnit(body, design, InvaderFaction, 0);

            // 3) The ground processor runs: with only the invader holding live units in region 0, it is CAPTURED.
            new GroundForcesProcessor().ProcessEntity(body, 3600);

            Assert.That(regionsDB.Regions[0].OwnerFactionID, Is.EqualTo(InvaderFaction),
                "with the garrison bombarded away and an invader landed, the region is taken — the full space→ground→capture chain");
            Log($"region 0 flipped to invader {InvaderFaction} after orbital bombardment cleared the defenders");
        }
    }
}
