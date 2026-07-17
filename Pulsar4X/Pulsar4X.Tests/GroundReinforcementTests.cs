using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pulsar4X.Components;   // ComponentDesign
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.GroundCombat;
using Pulsar4X.Ships;        // ShipDesign (the "not a ground unit" case)

namespace Pulsar4X.Tests
{
    /// <summary>
    /// GROUND REINFORCEMENT gauge — the ground echo of the fleet "size-to-resources + keep-a-reserve" ladder applied
    /// to a planetary GARRISON (the developer's call: a garrison shouldn't ship its WHOLE defense off on an invasion,
    /// and a depleted garrison should be REBUILT). Gauges the pure decision helper <see cref="GroundReinforcement"/>
    /// directly (the reserve/reinforcement MATH), plus the two integration reads the <c>ConquerResolver</c> wiring
    /// uses (<see cref="GroundReinforcement.GarrisonBodyOf"/> and <see cref="GroundReinforcement.IsBuildableGroundUnit"/>).
    ///
    /// The resolver-side wiring (the LOAD-rung reserve guard + the REBUILD rung) is kept byte-identical for a
    /// garrison-less faction, so the existing <c>ConquerResolverTests</c> / <c>MilitaryCompositionTests</c> (whose
    /// factions carry no home garrison) remain the byte-identity tripwire — this fixture proves the helper the wiring
    /// consults returns the right answers, without driving a full build-to-fielded sim (which the industry queue makes
    /// flaky in-harness — Tests/CLAUDE.md gotcha 7).
    /// </summary>
    [TestFixture]
    public class GroundReinforcementTests
    {
        /// <summary>A bare faction blob carrying an authored garrison composition (type-name → count).</summary>
        private static FactionInfoDB FactionWithGarrison(params (string type, int count)[] composition)
        {
            var fi = new FactionInfoDB();
            fi.GarrisonComposition = composition.ToDictionary(c => c.type, c => c.count);
            return fi;
        }

        /// <summary>A roster of <paramref name="count"/> units all owned by <paramref name="factionId"/>.</summary>
        private static GroundForcesDB RosterOf(int factionId, int count)
        {
            var forces = new GroundForcesDB();
            for (int i = 0; i < count; i++)
                forces.Units.Add(new GroundUnit { FactionOwnerID = factionId, UnitType = GroundUnitType.Infantry });
            return forces;
        }

        // ── Target + reserve derive from the faction's authored composition (no new data) ──────────────────────────

        [Test]
        [Description("The garrison TARGET is the sum of the authored composition; the RESERVE is half of it (floored at 1).")]
        public void TargetAndReserve_FromAuthoredComposition()
        {
            var light = FactionWithGarrison(("Infantry", 3), ("Armor", 2), ("Artillery", 1));   // 6
            Assert.That(GroundReinforcement.GarrisonTargetFor(light), Is.EqualTo(6), "target = 3+2+1");
            Assert.That(GroundReinforcement.GarrisonReserveFor(light), Is.EqualTo(3), "reserve = ceil(6 * 0.5)");

            var legion = FactionWithGarrison(("Infantry", 4), ("Armor", 3), ("Artillery", 2));   // 9 (the UMF's heavier mix)
            Assert.That(GroundReinforcement.GarrisonTargetFor(legion), Is.EqualTo(9), "a heavier authored legion → bigger target");
            Assert.That(GroundReinforcement.GarrisonReserveFor(legion), Is.EqualTo(5), "reserve = ceil(9 * 0.5)");
        }

        [Test]
        [Description("A faction with NO authored composition falls back to the engine default garrison (3/2/1 = 6) — byte-identical with GroundStartGarrison.")]
        public void NoAuthoredComposition_FallsBackToEngineDefault()
        {
            var fi = new FactionInfoDB();   // empty GarrisonComposition
            Assert.That(GroundReinforcement.GarrisonTargetFor(fi), Is.EqualTo(6), "engine default 3 Inf + 2 Armor + 1 Arty");
            Assert.That(GroundReinforcement.GarrisonReserveFor(fi), Is.EqualTo(3), "reserve of the default = 3");
        }

        [Test]
        [Description("A null faction is a safe no-op (0 target / 0 reserve) — the helper never throws from the hotloop.")]
        public void NullFaction_IsASafeNoOp()
        {
            Assert.That(GroundReinforcement.GarrisonTargetFor(null), Is.EqualTo(0));
            Assert.That(GroundReinforcement.GarrisonReserveFor(null), Is.EqualTo(0));
        }

        // ── CurrentGarrison — counts only this faction's units ────────────────────────────────────────────────────

        [Test]
        [Description("CurrentGarrison counts only the queried faction's units on the roster (a mixed/contested world is split correctly).")]
        public void CurrentGarrison_CountsOnlyThatFaction()
        {
            var forces = new GroundForcesDB();
            for (int i = 0; i < 4; i++) forces.Units.Add(new GroundUnit { FactionOwnerID = 42 });
            for (int i = 0; i < 2; i++) forces.Units.Add(new GroundUnit { FactionOwnerID = 7 });   // an enemy garrison on the same world

            Assert.That(GroundReinforcement.CurrentGarrison(forces, 42), Is.EqualTo(4), "only faction 42's units");
            Assert.That(GroundReinforcement.CurrentGarrison(forces, 7), Is.EqualTo(2), "only faction 7's units");
            Assert.That(GroundReinforcement.CurrentGarrison((GroundForcesDB)null, 42), Is.EqualTo(0), "null roster → 0");
        }

        // ── NeedsReinforcement — the REBUILD-rung trigger ─────────────────────────────────────────────────────────

        [Test]
        [Description("A FULL garrison needs no reinforcement; a DEPLETED-but-standing one does; a WIPED (0-unit) roster is a no-op (nothing to maintain).")]
        public void NeedsReinforcement_FullNo_DepletedYes_EmptyNo()
        {
            var fi = FactionWithGarrison(("Infantry", 3), ("Armor", 2), ("Artillery", 1));   // target 6
            const int factionId = 42;

            Assert.That(GroundReinforcement.NeedsReinforcement(RosterOf(factionId, 6), fi, factionId), Is.False,
                "a garrison at its target does not need rebuilding");
            Assert.That(GroundReinforcement.NeedsReinforcement(RosterOf(factionId, 4), fi, factionId), Is.True,
                "a garrison ground below its target (combat losses / units shipped off) needs rebuilding");
            Assert.That(GroundReinforcement.NeedsReinforcement(RosterOf(factionId, 0), fi, factionId), Is.False,
                "a world we don't garrison at all is NOT our reinforcement concern (the no-op guard)");
        }

        // ── WouldStripReserve — the LOAD-rung home-defense guard ──────────────────────────────────────────────────

        [Test]
        [Description("WouldStripReserve is TRUE at/under the reserve floor (hold the defenders) and FALSE with a surplus (a unit may ship off).")]
        public void WouldStripReserve_TrueAtReserve_FalseWithSurplus()
        {
            var fi = FactionWithGarrison(("Infantry", 3), ("Armor", 2), ("Artillery", 1));   // target 6, reserve 3
            const int factionId = 42;

            Assert.That(GroundReinforcement.WouldStripReserve(RosterOf(factionId, 4), fi, factionId), Is.False,
                "4 units > reserve 3 → a surplus exists, shipping one leaves the reserve intact");
            Assert.That(GroundReinforcement.WouldStripReserve(RosterOf(factionId, 3), fi, factionId), Is.True,
                "3 units == reserve → shipping one would breach the home reserve, so hold it (don't ship the last defenders)");
            Assert.That(GroundReinforcement.WouldStripReserve(RosterOf(factionId, 2), fi, factionId), Is.True,
                "already under the reserve → definitely hold");
        }

        // ── The Entity-overload reads the roster off a real body (what the resolver actually calls) ────────────────

        [Test]
        [Description("The Entity overloads read a real body's GroundForcesDB roster — the exact path the LOAD guard + REBUILD trigger use.")]
        public void EntityOverloads_ReadTheBodysRoster()
        {
            var s = TestScenario.CreateWithColony();
            var fi = s.Faction.GetDataBlob<FactionInfoDB>();
            fi.GarrisonComposition = new Dictionary<string, int> { { "Infantry", 3 }, { "Armor", 2 }, { "Artillery", 1 } };   // target 6, reserve 3

            var body = Entity.Create();
            s.StartingSystem.AddEntity(body);
            var forces = new GroundForcesDB();
            for (int i = 0; i < 4; i++) forces.Units.Add(new GroundUnit { FactionOwnerID = s.Faction.Id });
            body.SetDataBlob(forces);   // attach the roster (synchronous for the datablob store)

            Assert.That(GroundReinforcement.CurrentGarrison(body, s.Faction.Id), Is.EqualTo(4), "reads the 4 units off the body");
            Assert.That(GroundReinforcement.NeedsReinforcement(body, fi, s.Faction.Id), Is.True, "4 < target 6 → rebuild");
            Assert.That(GroundReinforcement.WouldStripReserve(body, fi, s.Faction.Id), Is.False, "4 > reserve 3 → a unit may still ship off");

            // A body with NO roster is a clean no-op for all three (the garrison-less world / station case → byte-identical).
            var bare = Entity.Create();
            s.StartingSystem.AddEntity(bare);
            Assert.That(GroundReinforcement.CurrentGarrison(bare, s.Faction.Id), Is.EqualTo(0));
            Assert.That(GroundReinforcement.NeedsReinforcement(bare, fi, s.Faction.Id), Is.False, "no roster → nothing to rebuild");
        }

        // ── The two resolver integration reads: colony→body, and "is this a buildable ground unit?" ───────────────

        [Test]
        [Description("GarrisonBodyOf resolves a colony to the planet body that hosts its GroundForcesDB (the roster the rebuild rung inspects).")]
        public void GarrisonBodyOf_ResolvesTheColonysPlanet()
        {
            var s = TestScenario.CreateWithColony();
            Assert.That(GroundReinforcement.GarrisonBodyOf(s.Colony), Is.EqualTo(s.StartingBody),
                "the colony's ColonyInfoDB.PlanetEntity is the garrison host body");
            Assert.That(GroundReinforcement.GarrisonBodyOf(null), Is.Null, "null colony → null body (defensive)");
        }

        [Test]
        [Description("IsBuildableGroundUnit recognises a base-mod ground UNIT design (a ComponentDesign carrying GroundUnitAtb) and rejects a warship — the rebuild rung's design filter.")]
        public void IsBuildableGroundUnit_RecognisesAGroundUnit_RejectsAWarship()
        {
            var s = TestScenario.CreateWithColony();
            var fi = s.Faction.GetDataBlob<FactionInfoDB>();

            // The base-mod infantry ground unit lives in IndustryDesigns as a ComponentDesign carrying GroundUnitAtb
            // (the exact set the rebuild rung iterates over) — see GroundUnitBaseModTests.
            var infantry = (ComponentDesign)fi.IndustryDesigns["default-design-infantry"];
            Assert.That(GroundReinforcement.IsBuildableGroundUnit(infantry), Is.True,
                "a ComponentDesign carrying GroundUnitAtb is a buildable ground unit");

            // And the rung's data source (IndustryDesigns) really does contain buildable ground units to pick.
            Assert.That(fi.IndustryDesigns.Values.Any(GroundReinforcement.IsBuildableGroundUnit), Is.True,
                "the faction can actually build a ground unit to reinforce with");

            // A warship ship-design is NOT a ground unit (so the rebuild rung never mistakes a hull for a garrison unit).
            var warship = fi.ShipDesigns.Values.First();
            Assert.That(GroundReinforcement.IsBuildableGroundUnit(warship), Is.False, "a ShipDesign is not a ground unit");
        }
    }
}
