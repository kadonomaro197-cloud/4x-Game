using System;
using System.Linq;
using NUnit.Framework;
using Pulsar4X.Datablobs;    // ComponentInstancesDB
using Pulsar4X.Engine;
using Pulsar4X.Factions;     // FactionInfoDB
using Pulsar4X.Fleets;       // FleetFactory, FleetDB
using Pulsar4X.GeoSurveys;   // GeoSurveyAtb
using Pulsar4X.GroundCombat; // GroundBayAtb
using Pulsar4X.Ships;        // ShipRoleTools (AbilitiesOf / CanIssue extensions), ShipDesign, ShipFactory
using Pulsar4X.Weapons;      // GenericBeamWeaponAtb

namespace Pulsar4X.Tests
{
    /// <summary>
    /// OPERATION BLUEPRINT-TO-STEEL A5 — the order → ability COMPONENT SCAN (FORCES-WINDOW-DESIGN §4.5).
    ///
    /// An order isn't a free-floating verb; it's powered by a COMPONENT, so the Forces window offers an order only when
    /// the unit carries the part. The engine already did this for exactly three hand-written cases (`HasGeoSurveyAbility`
    /// / `HasJPSurveyAbililty` + the client's troop-bay scan). A5 generalizes them into one shared pair:
    /// `ShipRoleTools.AbilitiesOf(entity)` (the set of `*Atb` attribute Types a unit carries — recursing a fleet's
    /// members) + `CanIssue(entity, orderKey)` over an `OrderAbilityTable`. Additive + read-only → a running game is
    /// byte-identical until a caller adopts it; these gauges pin the behavior.
    ///
    /// The load-bearing case is (d) the GRAVE RUNG: `ComponentInstancesDB.RemoveComponentInstance` leaves the empty
    /// attribute-Type key behind, so `AbilitiesOf` must filter on a live instance count or a shot-off part still grants
    /// its order. This test reproduces that exact stale state (an empty list under the key) and asserts the ability is
    /// gone — the one bug that would break cradle-to-grave.
    /// </summary>
    [TestFixture]
    public class AbilityScanTests
    {
        private static ShipDesign FirstDesignWith<T>(Entity faction) where T : Pulsar4X.Interfaces.IComponentDesignAttribute
            => faction.GetDataBlob<FactionInfoDB>().ShipDesigns.Values.FirstOrDefault(d => d.TryGetComponentsByAttribute<T>(out _));
        private static ShipDesign FirstDesignWithout<T>(Entity faction) where T : Pulsar4X.Interfaces.IComponentDesignAttribute
            => faction.GetDataBlob<FactionInfoDB>().ShipDesigns.Values.FirstOrDefault(d => !d.TryGetComponentsByAttribute<T>(out _));

        [Test]
        [Description("A ship carrying a survey sensor reports GeoSurveyAtb in AbilitiesOf and CanIssue(GeoSurvey); a fleet "
                   + "holding it reports the same (the FleetDB.Children union); a ship without one cannot; and clearing the "
                   + "component (the grave-rung stale-key state) removes the ability (the Count>0 filter).")]
        public void AbilitiesOf_ScansComponents_UnionsAcrossAFleet_AndHonoursTheGraveRung()
        {
            var s = TestScenario.CreateWithColony();

            var surveyorDesign = FirstDesignWith<GeoSurveyAtb>(s.Faction);
            Assert.That(surveyorDesign, Is.Not.Null, "the start faction fields a survey-capable design");
            var surveyor = ShipFactory.CreateShip(surveyorDesign, s.Faction, s.StartingBody);

            // (a) the ship's own scan
            Assert.That(surveyor.AbilitiesOf().Contains(typeof(GeoSurveyAtb)), Is.True,
                "a mounted survey sensor puts GeoSurveyAtb in the ability set");
            Assert.That(surveyor.CanIssue("GeoSurvey"), Is.True, "so the ship can issue Geo-Survey");

            // (b) a fleet's set is the UNION of its members'
            var fleet = FleetFactory.Create(s.StartingSystem, s.Faction.Id, "A5 Ability Test Fleet");
            fleet.GetDataBlob<FleetDB>().AddChild(surveyor);
            Assert.That(fleet.AbilitiesOf().Contains(typeof(GeoSurveyAtb)), Is.True,
                "the fleet inherits its member ship's survey ability (recursed FleetDB.Children)");
            Assert.That(fleet.CanIssue("GeoSurvey"), Is.True, "so the fleet can issue Geo-Survey");

            // (c) a ship with no survey part cannot
            var nonSurveyorDesign = FirstDesignWithout<GeoSurveyAtb>(s.Faction);
            Assert.That(nonSurveyorDesign, Is.Not.Null, "the start faction fields a non-survey design too");
            var nonSurveyor = ShipFactory.CreateShip(nonSurveyorDesign, s.Faction, s.StartingBody);
            Assert.That(nonSurveyor.CanIssue("GeoSurvey"), Is.False, "no survey sensor → no Geo-Survey order");

            // (d) THE GRAVE RUNG: reproduce the exact stale state RemoveComponentInstance leaves (the Type key present
            // with an EMPTY instance list) and assert the ability is gone — proves the Count>0 filter.
            var comps = surveyor.GetDataBlob<ComponentInstancesDB>();
            comps.ComponentsByAttribute[typeof(GeoSurveyAtb)].Clear();
            Assert.That(surveyor.AbilitiesOf().Contains(typeof(GeoSurveyAtb)), Is.False,
                "a fully-uninstalled survey sensor (empty list under the key) no longer grants the order — the grave rung");
            Assert.That(surveyor.CanIssue("GeoSurvey"), Is.False, "and the Geo-Survey order vanishes with the part");
        }

        [Test]
        [Description("The attribute-Type vocabulary subsumes the no-AbilityDB case: a troop bay (GroundBayAtb, which has "
                   + "NO *AbilityDB — the reason the client's troop-bay gate had to scan attributes) is reported by "
                   + "AbilitiesOf and gates the Embark-Troops order.")]
        public void AbilitiesOf_CoversTheTroopBay_WhichHasNoAbilityDB()
        {
            var s = TestScenario.CreateWithColony();
            var transportDesign = FirstDesignWith<GroundBayAtb>(s.Faction);
            if (transportDesign == null)
            {
                Assert.Ignore("the start faction has no troop-bay (GroundBayAtb) ship design — troop-bay case not exercised here");
                return;
            }
            var transport = ShipFactory.CreateShip(transportDesign, s.Faction, s.StartingBody);
            Assert.That(transport.AbilitiesOf().Contains(typeof(GroundBayAtb)), Is.True,
                "a mounted troop bay puts GroundBayAtb in the ability set (an attribute with no *AbilityDB)");
            Assert.That(transport.CanIssue("EmbarkTroops"), Is.True, "so the transport can issue Embark-Troops");
        }

        [Test]
        [Description("A warship's ability set contains its weapon attribute but not a survey one — the scan reads what is "
                   + "actually bolted on, nothing more.")]
        public void AbilitiesOf_ReadsTheRealParts_NoPhantomAbilities()
        {
            var s = TestScenario.CreateWithColony();
            var warshipDesign = FirstDesignWith<GenericBeamWeaponAtb>(s.Faction);
            Assert.That(warshipDesign, Is.Not.Null, "the start faction fields a beam warship");
            var warship = ShipFactory.CreateShip(warshipDesign, s.Faction, s.StartingBody);

            var abilities = warship.AbilitiesOf();
            Assert.That(abilities.Contains(typeof(GenericBeamWeaponAtb)), Is.True, "a beam warship carries its beam attribute");
            Assert.That(warship.CanIssue("FireControl"),
                Is.EqualTo(abilities.Contains(typeof(Pulsar4X.Weapons.BeamFireControlAtbDB))),
                "CanIssue(FireControl) tracks whether the ship actually carries a beam fire-control component");
        }
    }
}
