using System;
using NUnit.Framework;
using Pulsar4X.Combat;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Sensors;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// F-C3b gauge (docs/ESPIONAGE-AND-INTELLIGENCE-DESIGN.md): the intel↔eyes bridge — what confirming intel buys.
    /// With only Inferred Military intel (or no ledger) the observer reads a rival's strength through the FOG
    /// (ThreatAssessment.DetectedStrengthOf); once the Military facet is Confirmed it reads the TRUE strength
    /// (FactionRollup.MilitaryStrength). Pure/read-only → byte-identical.
    /// </summary>
    [TestFixture]
    public class IntelAssessmentTests
    {
        [Test]
        [Description("Inferred/null → the fog estimate; Confirming Military intel sharpens it into the rival's true strength.")]
        public void EstimatedMilitaryStrength_SharpensFromFogToTruth_OnConfirm()
        {
            var s = TestScenario.CreateWithColony();
            var game = s.Game;
            var observer = s.Faction;
            var observerInfo = observer.GetDataBlob<FactionInfoDB>();
            var sys = s.StartingSystem;

            // A rival faction with a real fleet → a known TRUE strength (Firepower 3000 + Toughness 2000 = 5000).
            var rival = FactionFactory.CreateBasicFaction(game, "Rival", "RIV", 0);
            int rivalId = rival.Id;
            var rivalShip = Entity.Create();
            rivalShip.FactionOwnerID = rivalId;
            sys.AddEntity(rivalShip);
            rivalShip.SetDataBlob(new ShipCombatValueDB(3000, 2000, 1.0));
            double trueStrength = FactionRollup.MilitaryStrength(rival);
            Assert.That(trueStrength, Is.EqualTo(5000.0).Within(1e-6));

            // The observer DETECTS one of the rival's ships (loudness 250) → a fog estimate distinct from the truth.
            var info = new SensorInfoDB { LatestDetectionQuality = new SensorReturnValues { SignalStrength_kW = 250.0 } };
            observerInfo.SensorContacts[rivalShip.Id] =
                new SensorContact { ActualEntity = rivalShip, ActualEntityId = rivalShip.Id, SensorInfo = info };
            double fog = ThreatAssessment.DetectedStrengthOf(observer, rivalId);
            Assert.That(fog, Is.EqualTo(250.0).Within(1e-6));

            var ledger = new InformationLedgerDB();

            // Inferred (default) and a null ledger → the fog estimate (the poker read).
            Assert.That(IntelAssessment.EstimatedMilitaryStrength(observer, rivalId, ledger), Is.EqualTo(fog).Within(1e-6),
                "Inferred Military intel → the fog-limited estimate");
            Assert.That(IntelAssessment.EstimatedMilitaryStrength(observer, rivalId, null), Is.EqualTo(fog).Within(1e-6),
                "no ledger → the fog-limited estimate");

            // Confirm Military intel → the TRUE strength (an agent got you the real number).
            ledger.Confirm(rivalId, IntelFacet.Military, new DateTime(2050, 1, 1));
            Assert.That(IntelAssessment.EstimatedMilitaryStrength(observer, rivalId, ledger), Is.EqualTo(trueStrength).Within(1e-6),
                "Confirmed Military intel → the rival's true strength");
        }
    }
}
