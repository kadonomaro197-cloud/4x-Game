using System.Linq;
using NUnit.Framework;
using Pulsar4X.Colonies;
using Pulsar4X.Damage;
using Pulsar4X.Engine;
using Pulsar4X.GroundCombat;
using Pulsar4X.Orbital;   // Vector2

namespace Pulsar4X.Tests
{
    /// <summary>
    /// The SPACE → GROUND connection the "you can take a planet" milestone needs (docs/MVP.md): orbital bombardment of
    /// a colony now SOFTENS its defending ground garrison, not just its population and installations. This is the step
    /// that lets a player grind a defender down from orbit before landing troops.
    ///
    /// Drives the real damage entry point (<see cref="DamageProcessor.OnTakingDamage"/> — the same method a beam/missile
    /// hit calls) on a colony carrying a real home garrison, and asserts (a) the DEFENDERS lose health and some die, and
    /// (b) a NON-defender (a landed invader's unit) is untouched — the friendly-fire guard. A colony with no garrison
    /// (every existing colony-damage test) is unaffected, so this is additive/byte-identical. Engine-only → runs in CI.
    /// </summary>
    [TestFixture]
    public class GroundBombardmentTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[bombard] " + m);

        [Test]
        [Description("Orbital bombardment of a colony softens its DEFENDING ground garrison (drains health + kills some units) while sparing a non-defender (a landed invader) — the space→ground 'soften before you land' link the MVP needs.")]
        public void OrbitalBombardment_SoftensTheDefendingGarrison_NotTheInvader()
        {
            var s = TestScenario.CreateWithColony();
            int raised = GroundStartGarrison.RaiseForFactionColonies(s.Game, s.Faction);
            Assert.That(raised, Is.GreaterThan(0), "a home garrison was raised on the colony's planet");

            var body = s.Colony.GetDataBlob<ColonyInfoDB>().PlanetEntity;
            Assert.That(body.TryGetDataBlob<GroundForcesDB>(out var forces), Is.True, "the planet body holds the garrison roster");
            int defenderFaction = s.Colony.FactionOwnerID;

            // A landed INVADER unit that bombardment aimed at the defender must NOT hit.
            forces.Units.Add(new GroundUnit { FactionOwnerID = 999999, Health = 500, MaxHealth = 500, RegionIndex = 0, Name = "Invader" });

            int defBefore = forces.Units.Count(u => u.FactionOwnerID == defenderFaction);
            double defHpBefore = forces.Units.Where(u => u.FactionOwnerID == defenderFaction).Sum(u => u.Health);
            Assert.That(defBefore, Is.GreaterThan(0), "there are defenders to soften");

            // A heavy orbital strike: 1e13 J → damageStrength ~100,000 → ~1000 raw health per defending unit — enough to
            // punch through the light garrison's small flat armour and kill units.
            var frag = new DamageFragment
            {
                Velocity = new Vector2(1, 0),   // non-zero (the harness's 1/|v| guard)
                Position = (0, 0),
                Mass = 1f, Density = 1000f, Momentum = 1f, Length = 1f,
                Energy = 1e13,
            };
            DamageProcessor.OnTakingDamage(s.Colony, frag);

            int defAfter = forces.Units.Count(u => u.FactionOwnerID == defenderFaction);
            double defHpAfter = forces.Units.Where(u => u.FactionOwnerID == defenderFaction).Sum(u => u.Health);
            var invader = forces.Units.FirstOrDefault(u => u.FactionOwnerID == 999999);
            Log($"defenders: {defBefore}→{defAfter} units, {defHpBefore:0}→{defHpAfter:0} hp; invader hp={invader?.Health}");

            Assert.That(defAfter, Is.LessThan(defBefore), "the bombardment KILLED some defending units (softened the garrison)");
            Assert.That(defHpAfter, Is.LessThan(defHpBefore), "...and drained the surviving defenders' health");
            Assert.That(invader, Is.Not.Null, "the invader unit is still present");
            Assert.That(invader.Health, Is.EqualTo(500.0),
                "a non-defender (a landed invader) is NOT hit by bombardment aimed at the defender — the friendly-fire guard");
        }

        [Test]
        [Description("Bombardment respects a ground unit's DEFENCES (via the same GroundDamageMatrix the ground resolver uses): a SHIELDED defender retains more health than an identical unshielded one under the same orbital strike — 'build defended ground units to survive the softening' is a real decision.")]
        public void OrbitalBombardment_ShieldedDefenderResistsBetter()
        {
            var s = TestScenario.CreateWithColony();
            var body = s.Colony.GetDataBlob<ColonyInfoDB>().PlanetEntity;
            int faction = s.Colony.FactionOwnerID;

            // Stand up two identical DEFENDING units — same faction, same HP, no armour — differing ONLY in shield.
            if (!body.TryGetDataBlob<GroundForcesDB>(out var forces))
            {
                forces = new GroundForcesDB();
                body.SetDataBlob(forces);
            }
            var bare    = new GroundUnit { FactionOwnerID = faction, RegionIndex = 0, MaxHealth = 1000, Health = 1000, Defense = 0, Shield = 0,   Name = "Bare" };
            var warded  = new GroundUnit { FactionOwnerID = faction, RegionIndex = 0, MaxHealth = 1000, Health = 1000, Defense = 0, Shield = 300, Name = "Warded" };
            forces.Units.Add(bare);
            forces.Units.Add(warded);

            // A moderate strike (5e12 J → ~500 raw): the shield soaks a fraction, so the warded unit keeps more health.
            var frag = new DamageFragment
            {
                Velocity = new Vector2(1, 0), Position = (0, 0),
                Mass = 1f, Density = 1000f, Momentum = 1f, Length = 1f,
                Energy = 5e12,
            };
            DamageProcessor.OnTakingDamage(s.Colony, frag);

            Log($"after strike — bare hp={bare.Health:0}, warded hp={warded.Health:0}");
            Assert.That(bare.Health, Is.LessThan(1000), "the unshielded defender was softened");
            Assert.That(warded.Health, Is.GreaterThan(bare.Health),
                "the SHIELDED defender kept more health under the same strike — its shield soaked part of the bombardment");
        }
    }
}
