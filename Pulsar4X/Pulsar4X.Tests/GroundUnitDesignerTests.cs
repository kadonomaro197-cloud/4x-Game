using System.Linq;
using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.GroundCombat;
using Pulsar4X.Components;
using Pulsar4X.Datablobs;   // ComponentInstancesDB (namespace ≠ folder)
using Pulsar4X.Factions;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// SHARED-DESIGNER track, slice A1 — a ground unit is a buildable COMPONENT carrying a <see cref="GroundUnitAtb"/>
    /// (CONVENTIONS §6: an ability is a component, not a parallel system), so it rides the SAME designer/research/industry
    /// rails a ship part does. The one thing it does differently: it's a MOBILE FORCE, so installing the finished
    /// component RAISES a <see cref="GroundUnit"/> on the colony's planet and then removes the transient installation
    /// (it became a force on the ground, not a colony building). This gauge exercises that install→raise hook directly,
    /// with hand-set stats — no base-mod template yet (that's slice A2). Design: docs/GROUND-COMBAT-MAP-DESIGN.md.
    /// </summary>
    [TestFixture]
    public class GroundUnitDesignerTests
    {
        // any real installed component carries the atb's hook in a New Game; here we borrow the bunker instance as the
        // transient carrier and fire the hook by hand with known stats (A2 supplies the real ground-unit template).
        private const string BunkerDesignId = "default-design-bunker";

        [Test]
        [Description("A1: installing a ground-unit component RAISES a unit on the planet from the atb's stats (type/attack/HP snapshot) and REMOVES the transient component (it deployed as a force, it isn't a lingering colony building).")]
        public void GroundUnitComponent_OnInstall_RaisesAUnit_AndRemovesTheComponent()
        {
            var s = TestScenario.CreateWithColony();
            var body = s.StartingBody;

            // a real installed component to stand in as the just-built ground-unit component (transient carrier)
            var fi = s.Faction.GetDataBlob<FactionInfoDB>();
            var design = (ComponentDesign)fi.IndustryDesigns[BunkerDesignId];
            var carrier = new ComponentInstance(design);
            s.Colony.AddComponent(carrier);
            var comps = s.Colony.GetDataBlob<ComponentInstancesDB>();
            Assert.That(comps.AllComponents.ContainsKey(carrier.UniqueID), Is.True, "precondition: the carrier component is installed");

            int unitsBefore = body.TryGetDataBlob<GroundForcesDB>(out var pre) ? pre.Units.Count : 0;

            // the atb with known stats — Armor, attack 42, defense 7, HP 300, range 2
            var atb = new GroundUnitAtb((double)(int)GroundUnitType.Armor, 42, 7, 300, 2);
            atb.OnComponentInstallation(s.Colony, carrier);

            Assert.That(body.TryGetDataBlob<GroundForcesDB>(out var forces), Is.True, "a roster now exists on the body");
            Assert.That(forces.Units.Count, Is.EqualTo(unitsBefore + 1), "exactly one unit was raised");
            var raised = forces.Units.Last();
            Assert.That(raised.UnitType, Is.EqualTo(GroundUnitType.Armor), "unit type carried through the atb");
            Assert.That(raised.Attack, Is.EqualTo(42), "attack snapshotted from the atb");
            Assert.That(raised.MaxHealth, Is.EqualTo(300), "HP snapshotted from the atb");
            Assert.That(raised.Range, Is.EqualTo(2), "strike range carried through the atb");
            Assert.That(raised.FactionOwnerID, Is.EqualTo(s.Colony.FactionOwnerID), "raised under the building faction");

            Assert.That(comps.AllComponents.ContainsKey(carrier.UniqueID), Is.False,
                "the transient component was removed — it deployed as a force, not a colony building");
        }

        [Test]
        [Description("A1 (defensive, L4): the install hook never throws even on a bad parent (no colony blob) — a hotloop/industry path must not crash the sim.")]
        public void GroundUnitComponent_OnInstall_NeverThrows_OnABadParent()
        {
            var atb = new GroundUnitAtb((double)(int)GroundUnitType.Infantry, 10, 5, 50, 1);
            Assert.DoesNotThrow(() => atb.OnComponentInstallation(null, null), "a null parent is skipped, not thrown");
        }
    }
}
