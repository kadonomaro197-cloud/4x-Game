using System.Collections.Generic;
using NUnit.Framework;
using Pulsar4X.Galaxy;        // PlanetRegionsFactory, PlanetRegionsDB, RegionFeatureType, PlanetHexFactory, GroundHex
using Pulsar4X.GroundCombat;  // GroundUnit(Design), GroundRole(Composer), GroundForces(Processor), GroundFormationDoctrine, PlanetEnvironmentsDB

namespace Pulsar4X.Tests
{
    /// <summary>
    /// W-TRACK W3 — SUB-FORMATION ROLE MANEUVER (docs/combat/GROUND-CLOSING-FIGHT-W-TRACK.md §W3). The ground echo of
    /// space sub-fleet roles: a ground unit's ROLE emerges from its stats (fast → Screen, long-reach → Artillery,
    /// no-punch → Support, else Line), and each role MANEUVERS DIFFERENTLY as it closes — a screen leads, a line holds
    /// at its range, an artillery kites to keep its standoff, support stays back. "Fighters move differently than a
    /// line ship," on the ground.
    ///
    /// Two pure gauges pin the decision logic (<see cref="GroundRoleComposer.ClassifyRole"/> / <c>RoleMoveAway</c>),
    /// and one wired gauge proves the resolver's maneuver step reads them behind the default-OFF
    /// <see cref="GroundForcesProcessor.EnableGroundRoleManeuver"/> flag (byte-identical when off — every existing
    /// ClosingFight gauge is unchanged). Engine-only → CI. Mirrors the closing-fight idiom of
    /// <c>GroundForcesTests.RangeCombat_OutRangerHitsCloserUnitFirst_CloneVsZerg</c>.
    /// </summary>
    [TestFixture]
    public class GroundRoleManeuverTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[w3-role] " + m);
        private const int Enemy = 990301;

        // ───────────────────────── the pure classifier ─────────────────────────
        [Test]
        [Description("ClassifyRole reads a unit's stats: no attack → Support; fast (high evasion) → Screen; long reach → "
                   + "Artillery; everything else → Line (same support→screen→artillery→line precedence as the space classifier).")]
        public void ClassifyRole_ByStats()
        {
            // no punch → Support, even if it's also fast + long-reach (support wins first)
            Assert.That(GroundRoleComposer.ClassifyRole(new GroundUnit { Attack = 0, Evasion = 0.9, Range = 3 }),
                Is.EqualTo(GroundRole.Support));
            // fast → Screen (before the range check)
            Assert.That(GroundRoleComposer.ClassifyRole(new GroundUnit { Attack = 100, Evasion = 0.6, Range = 1 }),
                Is.EqualTo(GroundRole.Screen));
            // long reach → Artillery
            Assert.That(GroundRoleComposer.ClassifyRole(new GroundUnit { Attack = 100, Evasion = 0.1, Range = 3 }),
                Is.EqualTo(GroundRole.Artillery));
            // ordinary → Line
            Assert.That(GroundRoleComposer.ClassifyRole(new GroundUnit { Attack = 100, Evasion = 0.1, Range = 1 }),
                Is.EqualTo(GroundRole.Line));
            Log("classify: no-attack→Support, evasive→Screen, long-range→Artillery, else→Line");
        }

        // ───────────────────────── the pure maneuver decision ─────────────────────────
        [Test]
        [Description("RoleMoveAway: a Screen closes to contact; a Line closes to its range then holds (never backs off); "
                   + "an Artillery closes if out of reach, KITES if the enemy gets inside its reach, holds at the edge; a "
                   + "Support backs off while the enemy can hit it. false=close, true=kite, null=hold.")]
        public void RoleMoveAway_EachRoleMovesDifferently()
        {
            // Screen — always pushes to contact (never kites); holds only when already there.
            Assert.That(GroundRoleComposer.RoleMoveAway(GroundRole.Screen, 5, 1, 1), Is.False, "screen closes");
            Assert.That(GroundRoleComposer.RoleMoveAway(GroundRole.Screen, 0, 1, 1), Is.Null, "screen at contact holds");

            // Line — closes to its firing range, then HOLDS (never opens the gap).
            Assert.That(GroundRoleComposer.RoleMoveAway(GroundRole.Line, 5, 2, 1), Is.False, "line out of range closes");
            Assert.That(GroundRoleComposer.RoleMoveAway(GroundRole.Line, 2, 2, 1), Is.Null, "line at its range holds");
            Assert.That(GroundRoleComposer.RoleMoveAway(GroundRole.Line, 1, 2, 1), Is.Null, "line inside its range holds (never backs off)");

            // Artillery — maintains its standoff: close if out of reach, kite if the enemy closes inside it, hold at the edge.
            Assert.That(GroundRoleComposer.RoleMoveAway(GroundRole.Artillery, 5, 3, 1), Is.False, "artillery out of reach closes");
            Assert.That(GroundRoleComposer.RoleMoveAway(GroundRole.Artillery, 1, 3, 1), Is.True, "artillery kites when the enemy is inside its reach");
            Assert.That(GroundRoleComposer.RoleMoveAway(GroundRole.Artillery, 3, 3, 1), Is.Null, "artillery holds at its max range");

            // Support — stays beyond the enemy's reach.
            Assert.That(GroundRoleComposer.RoleMoveAway(GroundRole.Support, 1, 1, 2), Is.True, "support backs off while the enemy can hit it");
            Assert.That(GroundRoleComposer.RoleMoveAway(GroundRole.Support, 5, 1, 2), Is.Null, "support safely out of reach holds");
            Log("maneuver: screen→close, line→close-then-hold, artillery→standoff-kite, support→keep-away");
        }

        // ───────────────────────── the wired maneuver (flag on) ─────────────────────────
        [Test]
        [Description("With EnableGroundRoleManeuver ON, two player units in ONE Close-to-Engage formation facing the same "
                   + "enemy 2 hexes away move in OPPOSITE directions by ROLE: the ARTILLERY (range 3) KITES away (where the "
                   + "uniform ROE would hold, since it's inside its range), the LINE (range 1) CLOSES in. Flag OFF → byte-identical.")]
        public void RoleManeuver_ArtilleryKitesWhileLineCloses()
        {
            GroundForcesProcessor.EnableGroundRoleManeuver = true;
            try
            {
                var s = TestScenario.CreateWithColony();
                PlanetRegionsFactory.GenerateForSystem(s.StartingSystem, surveyed: true);
                var body = s.StartingBody;
                if (body.HasDataBlob<PlanetEnvironmentsDB>()) body.RemoveDataBlob<PlanetEnvironmentsDB>();   // isolate maneuver from attrition
                var regionsDB = body.GetDataBlob<PlanetRegionsDB>();
                PlanetHexFactory.EnsureHexesForBody(body);
                foreach (var h in regionsDB.Regions[0].Hexes) h.Terrain = RegionFeatureType.Plains;   // deterministic, all-passable
                regionsDB.Regions[0].OwnerFactionID = -1;                                             // neutral ground

                GroundUnitDesign Design(string id, int range) => new GroundUnitDesign
                {
                    UniqueID = id, Name = id, UnitType = GroundUnitType.Infantry, Range = range,
                    Attack = 100, Defense = 10, HitPoints = 1000,
                    IndustryTypeID = "installation", ResourceCosts = new Dictionary<string, long>(),
                };

                var arty = GroundForces.RaiseUnit(body, Design("w3-arty", 3), s.Faction.Id, 0); arty.HexQ = 0; arty.HexR = 0;
                var line = GroundForces.RaiseUnit(body, Design("w3-line", 1), s.Faction.Id, 0); line.HexQ = 0; line.HexR = 0;
                // a punching-bag enemy 2 hexes away — Attack 0 (nobody dies), huge HP (survives the tick) so both maneuver against a live enemy.
                var bag = GroundForces.RaiseUnit(body, new GroundUnitDesign
                {
                    UniqueID = "w3-bag", Name = "Bag", UnitType = GroundUnitType.Infantry,
                    Attack = 0, Defense = 0, HitPoints = 1_000_000, Range = 1,
                    IndustryTypeID = "installation", ResourceCosts = new Dictionary<string, long>(),
                }, Enemy, 0);
                bag.HexQ = 2; bag.HexR = 0;

                var f = GroundForces.CreateFormation(body, s.Faction.Id, "Mixed Battery");
                GroundForces.AssignUnit(f, arty);
                GroundForces.AssignUnit(f, line);
                GroundFormationDoctrine.SetEngagementStance(f, GroundEngagementStance.CloseToEngage);

                Assert.That(GroundRoleComposer.ClassifyRole(arty), Is.EqualTo(GroundRole.Artillery), "precondition: the long gun is Artillery");
                Assert.That(GroundRoleComposer.ClassifyRole(line), Is.EqualTo(GroundRole.Line), "precondition: the short gun is Line");

                new GroundForcesProcessor().ProcessEntity(body, 1);   // 1s: the maneuver orders are issued but not yet walked

                int DistToBag(GroundHex h) => new Pulsar4X.Colonies.HexCoordinate(h.Q, h.R)
                    .DistanceTo(new Pulsar4X.Colonies.HexCoordinate(bag.HexQ, bag.HexR));

                Assert.That(arty.HexPath, Is.Not.Null.And.Not.Empty,
                    "the artillery maneuvered (a long gun kites at dist 2 inside its range 3 — where the uniform ROE would just hold)");
                Assert.That(line.HexPath, Is.Not.Null.And.Not.Empty, "the line maneuvered (it closes toward its short range)");
                var artyDest = arty.HexPath[arty.HexPath.Count - 1];
                var lineDest = line.HexPath[line.HexPath.Count - 1];
                Assert.That(DistToBag(artyDest), Is.GreaterThan(2), "the artillery kited AWAY (its next hex is farther from the enemy)");
                Assert.That(DistToBag(lineDest), Is.LessThan(2), "the line closed TOWARD the enemy");
                Log($"role maneuver @dist2: artillery→({artyDest.Q},{artyDest.R}) kite dist {DistToBag(artyDest)}; line→({lineDest.Q},{lineDest.R}) close dist {DistToBag(lineDest)}");
            }
            finally
            {
                GroundForcesProcessor.EnableGroundRoleManeuver = false;   // never leak the static flag to other tests
            }
        }
    }
}
