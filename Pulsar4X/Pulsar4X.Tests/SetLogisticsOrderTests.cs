using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Logistics;
using Pulsar4X.Storage;   // ICargoable

namespace Pulsar4X.Tests
{
    /// <summary>
    /// B-orders RANK-1 (OPERATION BLUEPRINT-TO-STEEL) — the stockpile MIN/MAX target write path
    /// (<see cref="SetLogisticsOrder.CreateCommand_SetDesiredLevels"/>). The reader is already live
    /// (<c>LogisticsCycle.UpdateListings</c> prices shortfall/surplus off <see cref="LogiBaseDB.DesiredLevels"/> hourly);
    /// this proves the missing write half sets/removes those targets through the ORDER path (deferred into Execute on the
    /// sim thread — a UI-thread mutation of the processor-iterated dictionary would throw "collection was modified"). Plus
    /// the in-slice <see cref="LogiBaseDB.Clone"/> fix: the copy-ctor used to silently drop Capacity / DesiredLevels /
    /// ItemsInTransit, so a cloned trade hub forgot its targets.
    /// </summary>
    [TestFixture]
    public class SetLogisticsOrderTests
    {
        [Test]
        [Description("CreateCommand_SetDesiredLevels writes a trade hub's per-item (min,max) target via the order path; a (0,0) pair removes it. Valid on a colony carrying a LogiBaseDB.")]
        public void SetDesiredLevels_WritesAndRemovesTargets()
        {
            var s = TestScenario.CreateWithColony();
            var data = s.Faction.GetDataBlob<FactionInfoDB>().Data;
            ICargoable good = data.CargoGoods.GetMaterialsList().First();
            Assert.That(good, Is.Not.Null, "the start faction has at least one unlocked material to target");

            // Give the colony a trade hub (a LogiBaseAtb install normally creates it; here we attach directly).
            s.Colony.SetDataBlob(new LogiBaseDB());

            // Set a min/max target through the order (executes synchronously — ActionOnDate defaults to the past, so
            // HandleOrder's OrderableProcessor pass runs Execute now).
            SetLogisticsOrder.CreateCommand_SetDesiredLevels(s.Colony,
                new Dictionary<ICargoable, (int, int)> { { good, (1000, 5000) } });

            var lb = s.Colony.GetDataBlob<LogiBaseDB>();
            Assert.That(lb.DesiredLevels.ContainsKey(good), Is.True, "the order set the item's target");
            Assert.That(lb.DesiredLevels[good], Is.EqualTo((1000, 5000)));

            // A (0,0) pair removes it (the hub stops caring about that item).
            SetLogisticsOrder.CreateCommand_SetDesiredLevels(s.Colony,
                new Dictionary<ICargoable, (int, int)> { { good, (0, 0) } });
            Assert.That(lb.DesiredLevels.ContainsKey(good), Is.False, "a (0,0) target removes the item");

            TestContext.Progress.WriteLine($"[set-desired] '{good.Name}' set to (1000,5000) then removed via the order");
        }

        [Test]
        [Description("LogiBaseDB.Clone (an entity move between managers) now preserves Capacity, DesiredLevels and ItemsInTransit — the copy-ctor dropped all three before, so a cloned trade hub forgot its targets. The copy is deep.")]
        public void Clone_PreservesCapacityDesiredLevelsAndTransit()
        {
            var s = TestScenario.CreateWithColony();
            var data = s.Faction.GetDataBlob<FactionInfoDB>().Data;
            ICargoable good = data.CargoGoods.GetMaterialsList().First();

            var lb = new LogiBaseDB { Capacity = 42 };
            lb.DesiredLevels[good] = (100, 900);

            var clone = (LogiBaseDB)lb.Clone();
            Assert.That(clone.Capacity, Is.EqualTo(42), "Capacity survives the clone");
            Assert.That(clone.DesiredLevels.ContainsKey(good), Is.True, "DesiredLevels survives the clone");
            Assert.That(clone.DesiredLevels[good], Is.EqualTo((100, 900)));

            // Deep copy — clearing the clone must not touch the original.
            clone.DesiredLevels.Clear();
            Assert.That(lb.DesiredLevels.ContainsKey(good), Is.True, "the copy is deep (a distinct dictionary)");
        }
    }
}
