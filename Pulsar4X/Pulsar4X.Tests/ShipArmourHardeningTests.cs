using System.Collections.Generic;
using NUnit.Framework;
using Pulsar4X.Combat;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Ships;
using CombatShip = Pulsar4X.Combat.CombatEngagement.CombatShip;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Defense ⚙3 — NATURE-HARDENED SHIP ARMOUR, the ship mirror of the ground armour-nature dials (developer's call:
    /// "mirror ground armor to space"). Plain ship armour folds into one flat toughness pool, blind to what's hitting
    /// it; an <see cref="ArmourHardeningAtb"/> plate soaks a fraction of the matching-NATURE incoming fire AFTER the
    /// shield — an ablative-clad cruiser shrugs off beams, a composite one walls kinetic. Read into
    /// <see cref="ShipCombatValueDB"/> and applied per-fleet (toughness-weighted) in <c>CombatEngagement</c>'s salvo
    /// resolve. <b>0 when absent → byte-identical</b> (every current ship). Engine-only → CI.
    /// </summary>
    [TestFixture]
    public class ShipArmourHardeningTests
    {
        private const string Ironclad = "default-ship-design-test-ironclad"; // Aegis + one Ablative Hull Plating
        private const string Aegis = "default-ship-design-test-warship";     // identical, plain armour
        private static void Log(string m) => TestContext.Progress.WriteLine("[armour-hardening] " + m);

        private static Entity Build(TestScenario s, string id, string name)
            => ShipFactory.CreateShip(s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns[id], s.Faction, s.StartingBody, name);

        [Test]
        [Description("The Ablative Hull Plating binds its four nature soak-fractions from JSON (the gotcha-10 sensor): the Ironclad reads soak vs energy 0.40 / vs kinetic 0.10, and a plain Aegis reads 0 across the board (byte-identical). Cradle-to-grave: JSON template → ArmourHardeningAtb → ShipCombatValueDB.")]
        public void TheAblativePlating_BindsItsNatureSoak_AndAPlainShipReadsZero()
        {
            var s = TestScenario.CreateWithColony();
            var iron = Build(s, Ironclad, "Ironclad").GetDataBlob<ShipCombatValueDB>();
            var plain = Build(s, Aegis, "Aegis").GetDataBlob<ShipCombatValueDB>();
            Log($"Ironclad soak — kinetic {iron.ArmourSoakVsKinetic:0.00}, energy {iron.ArmourSoakVsEnergy:0.00}; Aegis energy {plain.ArmourSoakVsEnergy:0.00}");

            Assert.That(iron.ArmourSoakVsEnergy, Is.EqualTo(0.40).Within(1e-6), "ablative soaks 40% of energy fire (from JSON)");
            Assert.That(iron.ArmourSoakVsKinetic, Is.EqualTo(0.10).Within(1e-6), "…but only 10% of a kinetic slug");
            Assert.That(plain.ArmourSoakVsEnergy, Is.EqualTo(0), "a plain-armour ship soaks nothing by nature (byte-identical)");
            Assert.That(plain.ArmourSoakVsKinetic, Is.EqualTo(0));
        }

        [Test]
        [Description("The fleet resolve reads it: a fleet of ablative Ironclads soaks ~40% of an all-ENERGY salvo but only ~10% of an all-KINETIC salvo (the matchup — pick ablative against beams), while a plain-Aegis fleet soaks 0 of either (byte-identical). This is the value applied after the shield in the salvo resolve.")]
        public void TheFleetResolve_SoaksEnergy_ButNotKinetic_ByThePlating()
        {
            var s = TestScenario.CreateWithColony();
            var ironFleet = new List<CombatShip> { new CombatShip(Build(s, Ironclad, "Iron1"), 1.0, 1.0) };
            var plainFleet = new List<CombatShip> { new CombatShip(Build(s, Aegis, "Plain1"), 1.0, 1.0) };

            var energySalvo = new List<WeaponProfile> { new WeaponProfile(100, 1e8, 1, 10, 0, WeaponNature.Energy, WeaponDelivery.Beam) };
            var kineticSalvo = new List<WeaponProfile> { new WeaponProfile(100, 5000, 0.1, 10, 0, WeaponNature.Kinetic, WeaponDelivery.Slug) };

            double ironVsEnergy = CombatEngagement.FleetArmourSoakFraction(ironFleet, energySalvo);
            double ironVsKinetic = CombatEngagement.FleetArmourSoakFraction(ironFleet, kineticSalvo);
            double plainVsEnergy = CombatEngagement.FleetArmourSoakFraction(plainFleet, energySalvo);
            Log($"ironclad fleet soaks: energy {ironVsEnergy:P0}, kinetic {ironVsKinetic:P0}; plain fleet energy {plainVsEnergy:P0}");

            Assert.That(ironVsEnergy, Is.EqualTo(0.40).Within(1e-6), "the ablative fleet soaks 40% of an all-energy salvo");
            Assert.That(ironVsKinetic, Is.EqualTo(0.10).Within(1e-6), "…but only 10% of an all-kinetic salvo — the matchup");
            Assert.That(ironVsEnergy, Is.GreaterThan(ironVsKinetic), "ablative is the anti-ENERGY choice");
            Assert.That(plainVsEnergy, Is.EqualTo(0), "a plain-armour fleet soaks nothing → byte-identical resolve");
        }
    }
}
