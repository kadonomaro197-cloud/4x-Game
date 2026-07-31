using System.Linq;
using NUnit.Framework;
using Pulsar4X.DataStructures;  // BodyType
using Pulsar4X.Extensions;      // GetDefaultName
using Pulsar4X.Galaxy;          // SystemBodyInfoDB
using Pulsar4X.Industry;        // MineralsDB

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Minerals on AUTHORED worlds. The authored body path (SystemBodyFactory.CreateFromBlueprint) only gave a
    /// body minerals if its JSON opted in — a "GenerateMinerals" preset (Earth/Mars/Mercury/Venus) or an explicit
    /// "Minerals" array (Luna). A body with NEITHER got NO MineralsDB, so its mines produced nothing. That silently
    /// starved TWO things: Titan (the Kithrin homeworld — no minerals = no fuel/materials = expansion stalls) and
    /// the whole OUTER Solar System (the Jovian/Saturnian moons + the dwarf planets carry a TYPO'd
    /// "MineralGeneration" key the loader ignores, so they were mineral-dead).
    ///
    /// The belt was ALSO barren by a SECOND route: the scattered rocks that fill Sol's main + Kuiper belts are
    /// made by StarSystemFactory.GenerateAsteroidBelt (NOT the authored-body path), and it never attached a
    /// MineralsDB — so the very bodies a player sends a mining ship to (140 main-belt + 90 Kuiper rocks) held no ore.
    ///
    /// Three fixes, verified here:
    ///   (1) Titan now authors an explicit hydrocarbon-rich deposit (fuel + metals for the Kithrin), routed through
    ///       the RNG-FREE MineralDepositFactory.Generate (no galaxy-gen stream perturbation).
    ///   (2) A SYSTEMIC FALLBACK on the authored-body path: any authored MINEABLE body with no mineral spec still
    ///       gets a deterministic body-type-abundance deposit (also RNG-free), so no authored world is ever
    ///       accidentally barren — while gas/ice giants are excluded (no surface).
    ///   (3) The SAME RNG-free fallback now runs for every scattered belt rock (GenerateAsteroidBelt), so the main
    ///       and Kuiper belts are mineable. RNG-free is what keeps the (commented-out) golden master safe AND leaves
    ///       the belt's own positions/masses byte-identical (it never draws the shared stream the scatter uses).
    /// </summary>
    [TestFixture]
    internal class MineralGenFallbackTests
    {
        [Test]
        [Description("Titan yields its explicit hydrocarbon deposit; every mineable body now has minerals (the " +
                     "systemic fallback rescues the barren outer system); gas giants stay barren (the body-type guard).")]
        public void AuthoredBodies_YieldMinerals_TitanHydrocarbons_FallbackFills_GasGiantsExcluded()
        {
            var s = TestScenario.CreateWithColony();
            var bodies = s.StartingSystem.GetAllEntitiesWithDataBlob<SystemBodyInfoDB>().ToList();
            int hydroId = s.Game.StartingGameData.Minerals["hydrocarbons"].ID;

            // (1) TITAN — the Kithrin homeworld — now yields its explicit hydrocarbon (fuel) deposit.
            var titan = bodies.FirstOrDefault(b => b.GetDefaultName() == "Titan");
            Assert.That(titan, Is.Not.Null, "Titan should be in the loaded Sol system");
            Assert.That(titan.HasDataBlob<MineralsDB>(), Is.True, "Titan now carries a mineral deposit");
            Assert.That(titan.GetDataBlob<MineralsDB>().Minerals.ContainsKey(hydroId), Is.True,
                "Titan's deposit includes hydrocarbons — the Kithrin's fuel source");

            // (2) THE SYSTEMIC FALLBACK — every MINEABLE body (moon / dwarf planet) now has a deposit, so the
            // formerly-barren outer Solar System is rescued (no authored world is accidentally mineral-dead).
            foreach (var body in bodies)
            {
                var t = body.GetDataBlob<SystemBodyInfoDB>().BodyType;
                if (t == BodyType.Moon || t == BodyType.DwarfPlanet)
                    Assert.That(body.HasDataBlob<MineralsDB>(), Is.True,
                        $"mineable body '{body.GetDefaultName()}' ({t}) should have minerals (explicit or via the fallback)");
            }

            // (3) THE BELT — the scattered belt rocks (main + Kuiper) are made by a SEPARATE path
            // (GenerateAsteroidBelt), so assert their coverage as a first-class invariant: at least one belt
            // Asteroid must now be mineable (they were ALL barren before the belt-path fix).
            var beltAsteroids = bodies
                .Where(b => b.GetDataBlob<SystemBodyInfoDB>().BodyType == BodyType.Asteroid)
                .ToList();
            Assert.That(beltAsteroids, Is.Not.Empty, "Sol should have scattered belt asteroids");
            Assert.That(beltAsteroids.Any(b => b.HasDataBlob<MineralsDB>()), Is.True,
                "belt asteroids (GenerateAsteroidBelt scatter) should now carry mineral deposits");

            // (4) THE GUARD — gas giants have no surface, so the fallback must EXCLUDE them (they stay barren).
            foreach (var body in bodies)
            {
                if (body.GetDataBlob<SystemBodyInfoDB>().BodyType == BodyType.GasGiant)
                    Assert.That(body.HasDataBlob<MineralsDB>(), Is.False,
                        $"gas giant '{body.GetDefaultName()}' must NOT be mineralized (no surface to mine)");
            }
        }
    }
}
