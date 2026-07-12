using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Hazards;
using Pulsar4X.Ships;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Defense ⚙5 ▸ HARDENING — a DRIVE REINFORCEMENT (S7). The last flagged Hazards hardening preset ("a Drive
    /// Reinforcement vs MovementDrag, a JSON template"): a component that reinforces a ship's sub-light drive against
    /// the MovementDrag of a thick medium (a gas/dust cloud, a debris field), so it powers through space that bogs
    /// others down. Pure DATA on the existing generic <see cref="HazardResistanceAtb"/> counter, consumed by
    /// <c>SpaceHazardProcessor</c> (drag reduced by resistance) via <see cref="SpaceHazardTools.ResistanceFraction"/>.
    ///
    /// With the warp stabilizer (S6) it completes the hardening preset trio — SensorJam (sensor-hardening-module),
    /// WarpInhibit (warp-stabilizer), MovementDrag (drive-reinforcement) are all counterable. Both live on the new
    /// Pathfinder Nebula Runner (a deep-space explorer needs both). Additive / byte-identical. Engine-only → CI.
    /// </summary>
    [TestFixture]
    public class ShipDriveReinforcementTests
    {
        private const string Pathfinder = "default-ship-design-test-pathfinder";
        private const string Aegis = "default-ship-design-test-warship";
        private static void Log(string m) => TestContext.Progress.WriteLine("[drive-reinforcement] " + m);

        private static Entity Build(TestScenario s, string designId, string name)
        {
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns;
            return ShipFactory.CreateShip(designs[designId], s.Faction, s.StartingBody, name);
        }

        [Test]
        [Description("Cradle-to-grave: the Pathfinder's drive reinforcement resists the MovementDrag hazard by its design fraction (0.6) through the real read path — the JSON drive-reinforcement template + design + earth.json entries wired up.")]
        public void ThePathfinder_ResistsMovementDrag()
        {
            var s = TestScenario.CreateWithColony();
            var ship = Build(s, Pathfinder, "Pathfinder");
            double resist = SpaceHazardTools.ResistanceFraction(ship, HazardEffectType.MovementDrag);
            Log($"Pathfinder MovementDrag resistance = {resist:0.00} (design 0.6)");
            Assert.That(resist, Is.EqualTo(0.6).Within(1e-9),
                "the drive reinforcement resists MovementDrag by its design fraction (JSON → HazardResistanceAtb wired)");
        }

        [Test]
        [Description("Two hardening modules coexist and are kind-specific: the Pathfinder resists BOTH MovementDrag AND WarpInhibit (each 0.6, from its two distinct modules), and a ship with no hardening resists neither — so each counter bites only its own hazard kind and they stack independently.")]
        public void TheTwoHardeningModules_Coexist_AndAreKindSpecific()
        {
            var s = TestScenario.CreateWithColony();
            var pathfinder = Build(s, Pathfinder, "Pathfinder");
            Assert.That(SpaceHazardTools.ResistanceFraction(pathfinder, HazardEffectType.MovementDrag), Is.EqualTo(0.6).Within(1e-9),
                "drive reinforcement → MovementDrag resistance");
            Assert.That(SpaceHazardTools.ResistanceFraction(pathfinder, HazardEffectType.WarpInhibit), Is.EqualTo(0.6).Within(1e-9),
                "warp stabilizer → WarpInhibit resistance (both modules coexist on one hull)");

            var aegis = Build(s, Aegis, "Aegis");
            Assert.That(SpaceHazardTools.ResistanceFraction(aegis, HazardEffectType.MovementDrag), Is.EqualTo(0.0),
                "a ship with no hardening resists nothing → byte-identical");
        }
    }
}
