using System.Linq;
using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Ships;
using Pulsar4X.Hazards;
using Pulsar4X.Damage;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Gauge on the DISCOVERY half of the survey→research loop: a faction's ship inside a hazard learns its damage
    /// flavour (recorded in <see cref="FactionHazardKnowledgeDB"/>), and that knowledge is what the research loop
    /// gates on. Drives <see cref="HazardDiscovery.RecordAndAnnounce"/> directly on a real faction + ship.
    /// </summary>
    [TestFixture]
    public class HazardDiscoveryTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[hazard-discovery] " + m);

        private static SpaceHazardDB ThermalHazard() => new SpaceHazardDB
        {
            HazardType = SpaceHazardType.StarCorona,
            Radius_m = 1e10,
            Effects = { new HazardEffect(HazardEffectType.HeatDamage, 100, 10000, scalesWithProximity: true) },
        };

        [Test]
        [Description("A faction's ship inside a hazard LEARNS its damage flavour: the faction gains a HazardKnowledge " +
                     "record of the signature. Idempotent — a second pass discovers nothing new.")]
        public void ShipInHazard_DiscoversItsSignature_Idempotent()
        {
            var s = TestScenario.CreateWithColony();
            var design = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns.Values.First();
            var ship = ShipFactory.CreateShip(design, s.Faction, s.StartingBody, "Surveyor");

            Assert.That(s.Faction.HasDataBlob<FactionHazardKnowledgeDB>(), Is.False,
                "no hazard knowledge before any encounter");

            var hazard = ThermalHazard();
            HazardDiscovery.RecordAndAnnounce(ship, hazard, s.Game.TimePulse.GameGlobalDateTime);

            Assert.That(s.Faction.TryGetDataBlob<FactionHazardKnowledgeDB>(out var knowledge), Is.True,
                "the faction should now hold hazard knowledge");
            Assert.That(knowledge.Knows(DamageSignature.Thermal), Is.True,
                "it should have discovered the thermal flavour");
            int countAfterFirst = knowledge.DiscoveredSignatures.Count;

            HazardDiscovery.RecordAndAnnounce(ship, hazard, s.Game.TimePulse.GameGlobalDateTime);
            Assert.That(knowledge.DiscoveredSignatures.Count, Is.EqualTo(countAfterFirst),
                "re-encountering the same hazard adds no new knowledge");
            Log($"discovered: {string.Join(", ", knowledge.DiscoveredSignatures)}");
        }

        [Test]
        [Description("Every damage signature maps to a counter-research tech id — the wire the discovery hook calls " +
                     "to unlock research when that flavour is first met.")]
        public void EverySignature_MapsToACounterTech()
        {
            foreach (DamageSignature sig in System.Enum.GetValues(typeof(DamageSignature)))
                Assert.That(HazardDiscovery.CounterTechFor(sig), Is.Not.Null, $"{sig} should map to a counter-tech");
        }
    }
}
