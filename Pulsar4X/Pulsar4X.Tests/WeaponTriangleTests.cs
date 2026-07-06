using NUnit.Framework;
using Pulsar4X.Combat;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Ships;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Combat depth P6 — the weapon TRIANGLE, demonstrated on REAL buildable example ships (docs/WEAPONS-AND-DODGE-
    /// DESIGN.md). Two purpose-built base-mod designs anchor the dodge axis:
    ///   • <c>default-ship-design-test-fighter</c> — "Wasp": tiny, 4 engines, 1 light gun. Small + agile = EVASIVE.
    ///   • <c>default-ship-design-test-capital</c> — "Leviathan": 4 railguns, 8 armour, 2 engines. Big + sluggish.
    /// (The "Aegis" beam warship and "Bulwark" flak escort already in the mod are the other two corners.)
    ///
    /// This proves the triangle EDGES the v1 dodge model expresses, on real ship combat values — no stamped numbers:
    ///   • FIGHTER ▸ railgun  — the fighter dodges slugs the capital eats.
    ///   • BEAM ▸ fighter      — light-speed ignores the very evasion that defeats the railgun.
    ///   • FLAK ▸ fighter      — saturation floors the dodge: the fighter-killer.
    /// (The CAPITAL ▸ beam edge needs weapon RANGE, a v1 stub — it's a v2 deepening, noted in the design doc.)
    /// Engine-only -> runs in CI. The fleets are spawnable from DevTools to watch the triangle live.
    /// </summary>
    [TestFixture]
    public class WeaponTriangleTests
    {
        private const string Fighter = "default-ship-design-test-fighter";
        private const string Capital = "default-ship-design-test-capital";
        private static void Log(string m) => TestContext.Progress.WriteLine("[triangle] " + m);

        [Test]
        [Description("On real built ships: the fighter is far more evasive than the capital, and that evasion is defeated by beams (light-speed) and flak (saturation) but NOT by railgun slugs — the dodge edges of the weapon triangle.")]
        public void RealDesigns_ExpressTheDodgeEdges_OfTheTriangle()
        {
            var s = TestScenario.CreateWithColony();
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns;
            Assert.That(designs.ContainsKey(Fighter), Is.True, "the Wasp fighter design loads onto the faction");
            Assert.That(designs.ContainsKey(Capital), Is.True, "the Leviathan capital design loads onto the faction");

            var fighter = ShipFactory.CreateShip(designs[Fighter], s.Faction, s.StartingBody, "Wasp").GetDataBlob<ShipCombatValueDB>();
            var capital = ShipFactory.CreateShip(designs[Capital], s.Faction, s.StartingBody, "Leviathan").GetDataBlob<ShipCombatValueDB>();
            Log($"fighter: ev={fighter.Evasion:0.###} tough={fighter.Toughness:0} fp={fighter.Firepower:0}; " +
                $"capital: ev={capital.Evasion:0.###} tough={capital.Toughness:0} fp={capital.Firepower:0}");

            // Small + high-thrust => evasive; big + sluggish + armoured => tanky but can't dodge.
            Assert.That(fighter.Evasion, Is.GreaterThan(capital.Evasion),
                "the small, high-thrust fighter is more evasive than the lumbering capital");
            Assert.That(fighter.Evasion, Is.GreaterThan(0), "a fighter with engines can dodge at all");
            Assert.That(capital.Toughness, Is.GreaterThan(fighter.Toughness),
                "the armoured capital out-toughs the paper-thin fighter (the other half of the trade)");

            // The three dodge edges, evaluated against the fighter's REAL evasion (and the capital's, for the slug).
            var beam = new WeaponProfile(1000, 3e8, 0.95, 0.5);      // ≈ light-speed
            var slug = new WeaponProfile(1000, 50_000, 0.05, 5);  // ballistic, low saturation
            var flak = new WeaponProfile(1000, 20_000, 0.10, 300);   // high saturation

            double beamVsFighter = CombatEngagement.HitFraction(beam, fighter.Evasion);
            double slugVsFighter = CombatEngagement.HitFraction(slug, fighter.Evasion);
            double flakVsFighter = CombatEngagement.HitFraction(flak, fighter.Evasion);
            double slugVsCapital = CombatEngagement.HitFraction(slug, capital.Evasion);
            Log($"vs fighter: beam={beamVsFighter:0.###} slug={slugVsFighter:0.###} flak={flakVsFighter:0.###}; slug vs capital={slugVsCapital:0.###}");

            Assert.That(slugVsFighter, Is.LessThan(slugVsCapital),
                "FIGHTER ▸ railgun: the fighter dodges slug fire the sluggish capital can't");
            Assert.That(beamVsFighter, Is.GreaterThan(slugVsFighter),
                "BEAM ▸ fighter: a light-speed beam ignores the evasion that defeats the railgun");
            Assert.That(flakVsFighter, Is.GreaterThan(slugVsFighter),
                "FLAK ▸ fighter: saturation floors the dodge — flak is the fighter-killer where a single slug isn't");
        }
    }
}
