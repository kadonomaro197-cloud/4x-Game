using NUnit.Framework;
using Pulsar4X.Combat;
using Pulsar4X.Datablobs;
using Pulsar4X.Factions;
using Pulsar4X.Ships;
using Pulsar4X.Weapons;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// MVP combat spine, step 2 — the ship "spec sheet" gauge (<see cref="ShipCombatValueDB"/>).
    ///
    /// Proves a built ship gets rated at build time: firepower (joules/sec from beams + a missile-launcher
    /// stub) and toughness (live components + armour) are computed and attached to the entity by
    /// <see cref="ShipFactory"/>.CreateShip. Reads the numbers into the CI log as [combat-value] lines so we
    /// can SEE what the real starting designs rate (Visibility Gate: build the gauge first).
    ///
    /// Engine-only -> runs in CI. The firepower assertion is conditional on the design actually carrying a
    /// beam weapon, so unarmed starting hulls (which legitimately rate 0 firepower) don't fail the gauge.
    /// </summary>
    [TestFixture]
    public class ShipCombatValueTests
    {
        private static void Log(string msg) => TestContext.Progress.WriteLine("[combat-value] " + msg);

        [Test]
        [Description("Every built ship is rated for combat: it has a ShipCombatValueDB with toughness > 0; firepower is read out, and > 0 for any design carrying a beam weapon.")]
        public void BuiltShip_GetsRated_FirepowerAndToughness()
        {
            var s = TestScenario.CreateWithColony();
            var factionInfo = s.Faction.GetDataBlob<FactionInfoDB>();
            Assert.That(factionInfo.ShipDesigns, Is.Not.Empty,
                "Faction has no ship designs to build from — the colony blueprint should unlock some.");

            int rated = 0;
            foreach (var design in factionInfo.ShipDesigns.Values)
            {
                var ship = ShipFactory.CreateShip(design, s.Faction, s.StartingBody, "Rating Test " + design.Name);

                Assert.That(ship.TryGetDataBlob<ShipCombatValueDB>(out var cv), Is.True,
                    $"Ship built from design '{design.Name}' has no ShipCombatValueDB — the build-time rating hook didn't fire.");

                // How many beam weapons does this design actually carry? Makes the firepower assertion
                // conditional on there being a weapon to fire.
                int beamCount = 0;
                if (ship.TryGetDataBlob<ComponentInstancesDB>(out var comps)
                    && comps.TryGetComponentsByAttribute<GenericBeamWeaponAtb>(out var beams))
                    beamCount = beams.Count;

                Log($"{design.Name}: firepower={cv.Firepower:0.###}  toughness={cv.Toughness:0.###}  role={cv.RoleWeight:0.##}  beams={beamCount}");

                Assert.That(cv.Toughness, Is.GreaterThan(0),
                    $"'{design.Name}' rated 0 toughness — every ship has components, so toughness should be > 0.");
                Assert.That(cv.Firepower, Is.GreaterThanOrEqualTo(0), "Firepower should never be negative.");

                if (beamCount > 0)
                    Assert.That(cv.Firepower, Is.GreaterThan(0),
                        $"'{design.Name}' carries {beamCount} beam weapon(s) but rated 0 firepower.");

                rated++;
            }

            Log($"rated {rated} starting design(s).");
            Assert.That(rated, Is.GreaterThan(0), "No designs were rated.");
        }
    }
}
