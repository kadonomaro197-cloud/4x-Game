using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Galaxy;
using Pulsar4X.GroundCombat;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Operation Earthfall C3.1 — the CROSS-BODY BATTALION REGISTRY contract the "Force Management" window's
    /// Battalions tab draws. The client enumerates every world carrying a <see cref="GroundForcesDB"/> and collects
    /// the player's formations via <see cref="GroundFormationTools.FormationsFor"/> (there is no engine cross-body
    /// helper yet — that's a GROUND follow-up), then shows each formation's aggregated strength / health / reach.
    /// This fixture pins that exact composition end-to-end: two formations raised on TWO different bodies are BOTH
    /// collected by the enumeration, their aggregate reads are correct, and an ENEMY formation is excluded (the
    /// player-only filter). Engine-only → runs in CI; the client draw itself is runtime-CI-blind, so this guards
    /// the engine contract it depends on. Design: docs/earthfall/findings/R1-manager-and-rings.md.
    /// </summary>
    [TestFixture]
    public class EfC3BattalionRegistryTests
    {
        private const int EnemyFaction = 987654;   // any id != the player's

        private static GroundUnitDesign Design(string id, GroundUnitType type, double attack, double hp, int range) => new GroundUnitDesign
        {
            UniqueID = id,
            Name = id,
            UnitType = type,
            Attack = attack,
            Defense = 10,
            HitPoints = hp,
            Range = range,
            IndustryPointCosts = 100,
            IndustryTypeID = "installation",
            ResourceCosts = new Dictionary<string, long>(),
        };

        [Test]
        [Description("C3.1: the Battalions tab collects the player's formations across MULTIPLE bodies (StarSystem.GetAllEntitiesWithDataBlob<GroundForcesDB> -> FormationsFor), aggregates strength/health/reach correctly, and excludes an enemy formation.")]
        public void BattalionRegistry_CollectsPlayerFormationsAcrossBodies_AndAggregates()
        {
            var s = TestScenario.CreateWithColony();
            int me = s.Faction.Id;

            // A second region-bearing world in the same system (Luna / Mars / ...).
            var earth = s.StartingBody;
            var sibling = s.StartingSystem.GetAllEntitiesWithDataBlob<PlanetRegionsDB>()
                .FirstOrDefault(b => b.Id != earth.Id);
            Assert.That(sibling, Is.Not.Null, "Sol has a second body with a region layer to garrison");

            // Earth: a 2-infantry battalion for the player.
            var inf = Design("c3-inf", GroundUnitType.Infantry, 100, 500, 1);
            var e1 = GroundForces.RaiseUnit(earth, inf, me, 0);
            var e2 = GroundForces.RaiseUnit(earth, inf, me, 0);
            var earthForm = GroundForces.CreateFormation(earth, me, "1st Guards");
            GroundForces.AssignUnit(earthForm, e1);
            GroundForces.AssignUnit(earthForm, e2);

            // Sibling: a mixed armour + artillery battalion for the player.
            var arm = Design("c3-arm", GroundUnitType.Armor, 140, 700, 1);
            var art = Design("c3-art", GroundUnitType.Artillery, 160, 400, 3);
            var b1 = GroundForces.RaiseUnit(sibling, arm, me, 0);
            var b2 = GroundForces.RaiseUnit(sibling, art, me, 0);
            var sibForm = GroundForces.CreateFormation(sibling, me, "2nd Line");
            GroundForces.AssignUnit(sibForm, b1);
            GroundForces.AssignUnit(sibForm, b2);

            // Earth also holds an ENEMY battalion (must NOT show in the player's registry).
            var en1 = GroundForces.RaiseUnit(earth, inf, EnemyFaction, 0);
            var enemyForm = GroundForces.CreateFormation(earth, EnemyFaction, "Invaders");
            GroundForces.AssignUnit(enemyForm, en1);

            // === The CLIENT's cross-body registry pattern, exercised on the engine ===
            var registry = new List<(Entity body, GroundForcesDB forces, GroundFormation formation)>();
            foreach (var body in s.StartingSystem.GetAllEntitiesWithDataBlob<GroundForcesDB>())
            {
                if (!body.TryGetDataBlob<GroundForcesDB>(out var forces)) continue;
                foreach (var f in GroundFormationTools.FormationsFor(forces, me))
                    registry.Add((body, forces, f));
            }

            // Both player formations collected, across two DIFFERENT bodies; the enemy one excluded.
            Assert.That(registry.Count, Is.EqualTo(2), "both of the player's battalions are collected across the two bodies");
            Assert.That(registry.Select(r => r.body.Id).Distinct().Count(), Is.EqualTo(2), "they live on two different bodies");
            Assert.That(registry.Any(r => r.formation.Name == "Invaders"), Is.False, "the enemy formation is NOT in the player's registry");

            // Aggregate reads the table column shows are correct (the ground echo of the fleet-combat-sheet totals).
            // NOTE: FormationId is a PER-BODY counter (GroundForces.CreateFormation → forces.NextFormationId++), so
            // earthForm and sibForm both get id 0 on their respective bodies — a row must be keyed by (body, formation),
            // NOT FormationId alone (which is exactly how the client's Battalions tab disambiguates selection:
            // _selBattalionBodyId + _selBattalionFormationId). Each body here has one player formation, so body.Id keys it.
            var earthRow = registry.First(r => r.body.Id == earth.Id);
            Assert.That(GroundFormationTools.MemberCount(earthRow.forces, earthRow.formation), Is.EqualTo(2));
            Assert.That(GroundFormationTools.FormationStrength(earthRow.forces, earthRow.formation), Is.EqualTo(200).Within(0.01), "2x infantry Attack 100 = 200");
            var (ec, em) = GroundFormationTools.FormationHealth(earthRow.forces, earthRow.formation);
            Assert.That(ec, Is.EqualTo(1000).Within(0.01));
            Assert.That(em, Is.EqualTo(1000).Within(0.01));
            Assert.That(GroundFormationTools.FormationReachHexes(earthRow.forces, earthRow.formation), Is.EqualTo(1), "infantry reach 1 hex");
            Assert.That(GroundForces.LeaderRegion(earthRow.forces, earthRow.formation), Is.EqualTo(0), "the battalion rallies in region 0");

            var sibRow = registry.First(r => r.body.Id == sibling.Id);
            Assert.That(GroundFormationTools.FormationStrength(sibRow.forces, sibRow.formation), Is.EqualTo(300).Within(0.01), "armour 140 + artillery 160 = 300");
            Assert.That(GroundFormationTools.FormationReachHexes(sibRow.forces, sibRow.formation), Is.EqualTo(3), "the artillery's 3-hex reach is the formation's (max)");

            TestContext.Progress.WriteLine($"[c3] registry collected {registry.Count} battalion(s) across "
                + $"{registry.Select(r => r.body.Id).Distinct().Count()} world(s); enemy excluded");
        }
    }
}
