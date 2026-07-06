using NUnit.Framework;
using Pulsar4X.Combat;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Ships;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Space SHIELD layer Phase C — the base-mod DEFLECTOR ARRAY (a shield generator) through the REAL data path, the
    /// shield twin of <see cref="RailgunWeaponTests"/>. The live game builds components from JSON (template → NCalc →
    /// Atb via reflection), which the C# test start normally skips — so this fixture builds the base-mod
    /// <c>default-ship-design-test-shielded</c> (Bastion) the way <see cref="TestScenario"/> loads the colony, exercising
    /// the JSON <c>deflector-array</c> template → <c>Pulsar4X.Combat.ShieldAtb</c> constructor binding →
    /// <see cref="ShipCombatValueDB"/> shield-pool read. If the template args/constructor drift this (and
    /// <c>BaseModIntegrityTests</c>) go red in CI instead of crashing the developer's New Game.
    ///
    /// It closes the shield's cradle-to-grave: a researched → built → installed generator projects a real pool the
    /// Phase-B resolver drains. And it re-proves the ADDITIVE property on real hulls — an unshielded ship reads 0.
    /// Engine-only → runs in CI.
    /// </summary>
    [TestFixture]
    public class ShieldBaseModTests
    {
        private const string ShieldedShip = "default-ship-design-test-shielded"; // Bastion — 2 × deflector-array
        private const string UnshieldedShip = "default-ship-design-test-warship"; // Aegis — no deflector
        private static void Log(string m) => TestContext.Progress.WriteLine("[shield-basemod] " + m);

        [Test]
        [Description("The base-mod Bastion cruiser builds from JSON and its real deflector-array components project a shield pool into the combat value (JSON deflector-array template → ShieldAtb → ShipCombatValueDB), while an unshielded Aegis reads a 0 pool (additive / byte-identical).")]
        public void DeflectorDesign_BuildsRealComponent_AndProjectsAShieldPool()
        {
            var s = TestScenario.CreateWithColony();
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns;
            Assert.That(designs.ContainsKey(ShieldedShip), Is.True,
                "the Bastion shielded cruiser loads onto the faction — the JSON deflector-array template + component design + ship design wired up");

            // Build it the real way (this instantiates the ShieldAtb from JSON via reflection).
            var ship = ShipFactory.CreateShip(designs[ShieldedShip], s.Faction, s.StartingBody, "Bastion");
            var cv = ship.GetDataBlob<ShipCombatValueDB>();
            Log($"Bastion: firepower={cv.Firepower:0}, shieldCapacity={cv.ShieldCapacity_J:0} J, shieldRegen={cv.ShieldRegen_Jps:0} J/s");

            Assert.That(cv.ShieldCapacity_J, Is.GreaterThan(0),
                "the deflector's pool flows into the combat value — JSON deflector-array template → ShieldAtb → ShipCombatValueDB is wired");
            Assert.That(cv.ShieldRegen_Jps, Is.GreaterThan(0), "and its recharge rate binds too");
            // Health-scaling cancels in the ratio, so this pins BOTH design values regardless of build health:
            // Recharge Rate 100 kJ/s ÷ Shield Capacity 5 MJ = 0.02.
            Assert.That(cv.ShieldRegen_Jps / cv.ShieldCapacity_J, Is.EqualTo(0.02).Within(1e-6),
                "regen:capacity ratio matches the design (100 kJ/s per 5 MJ)");
            Assert.That(cv.ShieldCapacity_J, Is.GreaterThanOrEqualTo(5_000_000),
                "two 5 MJ deflectors give a multi-MJ pool (the Bastion mounts 2)");
            Assert.That(cv.Firepower, Is.GreaterThan(0), "the Bastion still carries its lasers (an energy warship WITH a shield)");

            // ADDITIVE: a real ship with no deflector reads a 0 pool → combat byte-identical.
            var aegis = ShipFactory.CreateShip(designs[UnshieldedShip], s.Faction, s.StartingBody, "Aegis");
            var aegisCv = aegis.GetDataBlob<ShipCombatValueDB>();
            Log($"Aegis (no deflector): shieldCapacity={aegisCv.ShieldCapacity_J:0} J");
            Assert.That(aegisCv.ShieldCapacity_J, Is.EqualTo(0),
                "a ship with no deflector reads a 0 pool — combat is byte-identical for the unshielded");
        }
    }
}
