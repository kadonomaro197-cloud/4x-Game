using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Components;
using Pulsar4X.DataStructures;
using Pulsar4X.GroundCombat;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// TRANSPORT &amp; INVASION track (T1) — the lift half of "you can take a planet": a ship with a bay carries ground
    /// units off-world so an army built at home can reach an enemy world. T1a (this file, for now) proves the base-mod
    /// **troop bay** loads from JSON and binds its <see cref="GroundBayAtb"/> — the gotcha-10 sensor for the new ship
    /// component (the <see cref="RailgunWeaponTests"/> equivalent). T1b adds the load→fly→land round-trip on top.
    /// Engine-only → runs in CI. Design: docs/GROUND-COMBAT-MAP-DESIGN.md → transport.
    /// </summary>
    [TestFixture]
    public class GroundTransportTests
    {
        private const string TroopBay = "default-design-troop-bay";
        private static void Log(string m) => TestContext.Progress.WriteLine("[transport] " + m);

        [Test]
        [Description("T1a: the base-mod troop bay loads onto the start faction, binds a GroundBayAtb from JSON (Personnel class, non-zero capacity), and mounts as a ShipComponent so it can go on a transport ship.")]
        public void TroopBayDesign_LoadsFromJson_BindsTheBayAtb_AsAShipComponent()
        {
            var s = TestScenario.CreateWithColony();
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().IndustryDesigns;

            Assert.That(designs.ContainsKey(TroopBay), Is.True,
                "the troop bay loads onto the faction (template + design + earth.json entry wired up)");
            var design = designs[TroopBay] as ComponentDesign;
            Assert.That(design, Is.Not.Null, "the troop bay is a ComponentDesign (rides the shared designer)");

            Assert.That(design.HasAttribute<GroundBayAtb>(), Is.True,
                "the JSON groundBayAtbArgs bound a GroundBayAtb (gotcha-10 template→atb path works)");
            var bay = design.GetAttribute<GroundBayAtb>();
            Log($"{TroopBay}: capacity={bay.Capacity:0} class={bay.CarryClass} mount={design.ComponentMountType}");

            Assert.That(bay.CarryClass, Is.EqualTo(GroundCarryClass.Personnel), "a troop bay carries personnel");
            Assert.That(bay.Capacity, Is.GreaterThan(0), "it has real carry-room");
            Assert.That(design.ComponentMountType.HasFlag(ComponentMountType.ShipComponent), Is.True,
                "it mounts as a ShipComponent — so it can be installed on a transport ship");
        }
    }
}
