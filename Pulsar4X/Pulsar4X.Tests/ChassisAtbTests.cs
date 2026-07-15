using NUnit.Framework;
using Pulsar4X.Interfaces;
using Pulsar4X.Ships;
using Pulsar4X.GroundCombat;
using Pulsar4X.DataStructures;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// The PURELY ADDITIVE "chassis provides the budget" abstraction (2026-07-14). A ship hull and a ground
    /// chassis are both structural FRAMES that answer "how much can my mounted parts carry?" — so both now
    /// implement the shared <see cref="IChassisAtb"/> as [JsonIgnore] COMPUTED getters over the property they
    /// already store (hull → <see cref="ShipHullAtb.MassBudget"/>, chassis → <see cref="GroundChassisAtb.BaseStrength"/>).
    /// No backing field, no new JSON, no rename — save-safe. These gauges prove the mapping is correct and that
    /// the new <see cref="ComponentMountType.Station"/> flag is a real, distinct bit.
    /// </summary>
    [TestFixture]
    public class ChassisAtbTests
    {
        [Test]
        [Description("A ship hull IS a chassis whose budget is its MassBudget, measured in Mass, with ship-component parts.")]
        public void ShipHull_IsAChassis_BudgetIsMassBudget()
        {
            var hull = new ShipHullAtb(90000.0); // 90 t mass budget

            Assert.That(hull, Is.InstanceOf<IChassisAtb>(), "a ship hull is a chassis");

            IChassisAtb chassis = hull;
            Assert.That(chassis.BudgetKind, Is.EqualTo(ChassisBudgetKind.Mass), "a hull budgets in kilograms of mass");
            Assert.That(chassis.StructuralBudget, Is.EqualTo(hull.MassBudget), "the budget IS the hull's MassBudget");
            Assert.That(chassis.StructuralBudget, Is.EqualTo(90000.0));
            Assert.That(chassis.PartMount, Is.EqualTo(ComponentMountType.ShipComponent), "its parts use the ship-component mount");
        }

        [Test]
        [Description("A ground chassis IS a chassis whose budget is its BaseStrength, measured in Carry, with ground-unit parts.")]
        public void GroundChassis_IsAChassis_BudgetIsBaseStrength()
        {
            // ctor order: baseStrength, baseHP, size, locomotion, carryClass
            var frame = new GroundChassisAtb(500.0, 100.0, 10.0, 0.0, 0.0);

            Assert.That(frame, Is.InstanceOf<IChassisAtb>(), "a ground chassis is a chassis");

            IChassisAtb chassis = frame;
            Assert.That(chassis.BudgetKind, Is.EqualTo(ChassisBudgetKind.Carry), "a ground frame budgets in carry-strength");
            Assert.That(chassis.StructuralBudget, Is.EqualTo(frame.BaseStrength), "the budget IS the frame's BaseStrength");
            Assert.That(chassis.StructuralBudget, Is.EqualTo(500.0));
            Assert.That(chassis.PartMount, Is.EqualTo(ComponentMountType.GroundUnit), "its parts use the ground-unit mount");
        }

        [Test]
        [Description("The additive ComponentMountType.Station flag exists as its own distinct single-bit value (1 << 7), separate from every existing mount.")]
        public void ComponentMountType_Station_ExistsAndIsDistinct()
        {
            Assert.That((int)ComponentMountType.Station, Is.EqualTo(1 << 7), "Station is the 8th flag bit (128)");

            // distinct from every pre-existing mount value
            Assert.That(ComponentMountType.Station, Is.Not.EqualTo(ComponentMountType.None));
            Assert.That(ComponentMountType.Station, Is.Not.EqualTo(ComponentMountType.ShipComponent));
            Assert.That(ComponentMountType.Station, Is.Not.EqualTo(ComponentMountType.ShipCargo));
            Assert.That(ComponentMountType.Station, Is.Not.EqualTo(ComponentMountType.PlanetInstallation));
            Assert.That(ComponentMountType.Station, Is.Not.EqualTo(ComponentMountType.PDC));
            Assert.That(ComponentMountType.Station, Is.Not.EqualTo(ComponentMountType.Fighter));
            Assert.That(ComponentMountType.Station, Is.Not.EqualTo(ComponentMountType.Missile));
            Assert.That(ComponentMountType.Station, Is.Not.EqualTo(ComponentMountType.GroundUnit));

            // a clean single bit — no overlap with the flags below it
            var below = ComponentMountType.ShipComponent | ComponentMountType.ShipCargo | ComponentMountType.PlanetInstallation
                      | ComponentMountType.PDC | ComponentMountType.Fighter | ComponentMountType.Missile | ComponentMountType.GroundUnit;
            Assert.That(ComponentMountType.Station & below, Is.EqualTo((ComponentMountType)0), "Station shares no bit with the existing mounts");
        }
    }
}
