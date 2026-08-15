using System.Linq;
using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Ships;
using Pulsar4X.Datablobs;    // ComponentInstancesDB
using Pulsar4X.Components;   // ComponentInstance

namespace Pulsar4X.Tests
{
    /// <summary>
    /// B-S8 (OPERATION BLUEPRINT-TO-STEEL) — the missing ship HEALTH gauge for the Force-Management roster's Health
    /// column (FORCES-WINDOW-DESIGN §S8, decision #4: build the aggregate accessor engine-side so it's CI-tested, then
    /// the client just reads it). <see cref="ShipHealth.HealthFraction"/> sums a ship's living components'
    /// <c>HealthPercent</c> over the ORIGINAL design count, so a destroyed (removed) component counts as 0 — the honest
    /// choice a mean-of-survivors would hide.
    /// </summary>
    [TestFixture]
    public class ShipHealthTests
    {
        [Test]
        [Description("HealthFraction: a pristine ship reads 1.0; damaging one component to 50% drops it by 0.5/count; a DESTROYED (removed) component counts as 0 (denominator is the original design count, not the survivors).")]
        public void HealthFraction_PristineIsFull_DamageAndDestructionDropIt()
        {
            var s = TestScenario.CreateWithColony();
            var design = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns.Values.First();
            var ship = ShipFactory.CreateShip(design, s.Faction, s.StartingBody, "Health Gauge Ship");

            // A freshly built ship is at full health (this also asserts every design component instantiated 1:1).
            Assert.That(ShipHealth.HealthFraction(ship), Is.EqualTo(1.0).Within(1e-9),
                "a freshly built ship is at full health");

            var comps = ship.GetDataBlob<ComponentInstancesDB>();
            int designCount = design.Components.Sum(c => c.count);
            Assert.That(designCount, Is.GreaterThan(1), "the design has at least two components (needed for the removal case)");
            // The accessor divides by the ORIGINAL design count; a built ship instantiates its design 1:1, so the live
            // count equals it at birth. Asserting this makes the exact damage/destruction expectations below robust
            // (the accessor clamps to 1.0, which would otherwise hide a count mismatch on the pristine read).
            Assert.That(comps.AllComponents.Count, Is.EqualTo(designCount), "a fresh ship instantiates every design component 1:1");

            // Damage ONE component to 50% → health drops by exactly 0.5 / design-count.
            var first = comps.AllComponents.Values.First();
            first.HealthPercent = 0.5f;
            double expectedDamaged = (designCount - 0.5) / designCount;
            Assert.That(ShipHealth.HealthFraction(ship), Is.EqualTo(expectedDamaged).Within(1e-6),
                "a half-damaged component drops health by 0.5 / component-count");

            // DESTROY (remove) a DIFFERENT, still-full component → it counts as 0 because the denominator stays the
            // ORIGINAL design count. Live now: the 0.5 one + (designCount-2) full → sum = designCount - 1.5.
            var second = comps.AllComponents.Values.First(c => !ReferenceEquals(c, first));
            comps.RemoveComponentInstance(second);
            double expectedDestroyed = (designCount - 1.5) / designCount;
            Assert.That(ShipHealth.HealthFraction(ship), Is.EqualTo(expectedDestroyed).Within(1e-6),
                "a destroyed (removed) component counts as 0 — NOT hidden by a mean-of-survivors");

            TestContext.Progress.WriteLine($"[ship-health] {designCount} comps: pristine 1.0 -> damaged {expectedDamaged:0.###} -> destroyed {expectedDestroyed:0.###}");
        }

        [Test]
        [Description("HealthFraction is defensive: a null entity and a component-less entity both read 1.0 (a bare hull is undamaged, not 0 health) and never throw.")]
        public void HealthFraction_Defensive_NullAndComponentless()
        {
            Assert.That(ShipHealth.HealthFraction(null), Is.EqualTo(1.0), "null entity → 1.0, no throw");
            var bare = Entity.Create();
            Assert.That(ShipHealth.HealthFraction(bare), Is.EqualTo(1.0), "no ComponentInstancesDB → 1.0, no throw");
        }
    }
}
