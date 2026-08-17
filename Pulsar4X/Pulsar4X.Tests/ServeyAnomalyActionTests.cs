using System;
using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Fleets;
using Pulsar4X.JumpPoints;   // JPSurveyAbilityDB, JPSurveyableDB
using Pulsar4X.Ships;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// OPERATION BLUEPRINT-TO-STEEL B6 — the ONE real order-stub build. A4 de-fanged <see cref="ServeyAnomalyAction"/>
    /// (no crash / no wedge); B6 gives it real behaviour: "survey the nearest anomaly." An anomaly IS a
    /// <see cref="JPSurveyableDB"/> (a gravitational survey point — every generated system spawns ~30), so this order
    /// does NOT duplicate the working survey machinery — it FINDS the nearest un-surveyed anomaly in the fleet's own
    /// system and dispatches the same two orders the client's JP-survey menu issues (a fleet warp there, then a
    /// <see cref="JPSurveyOrder"/>). It's the combined "go survey the nearest anomaly" convenience neither existing
    /// order gives, and the same primitive a player and the AI can both issue (one verb, both seats).
    ///
    /// (The other three B6 stubs stay as-is by design: Refuel/Resupply "from own cargo" are NO-OPS in this engine —
    /// a ship's burnable fuel IS its fuel cargo, and a launcher fires straight from the ordnance hold with no physical
    /// magazine to top up — and their real external-source ops already exist as the client's "Refuel/Rearm at a base"
    /// buttons; Ship-Logistics' membership toggle already exists as SetLogisticsOrder. Building any of them would be a
    /// parallel/duplicate system.)
    /// </summary>
    [TestFixture]
    public class ServeyAnomalyActionTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[survey-anomaly] " + m);

        /// <summary>A fleet under the player carrying one corvette made survey-capable (a JP-survey ability blob).</summary>
        private static Entity MakeSurveyFleet(TestScenario s, string name)
        {
            var fleet = FleetFactory.Create(s.StartingSystem, s.Faction.Id, name);
            var design = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns["default-ship-design-test-corvette"];
            var ship = ShipFactory.CreateShip(design, s.Faction, s.StartingBody, name + " surveyor");
            s.Game.OrderHandler.HandleOrder(FleetOrder.AssignShip(s.Faction.Id, fleet, ship));
            ship.SetDataBlob(new JPSurveyAbilityDB { Speed = 5 });   // now the fleet can survey a grav anomaly
            return fleet;
        }

        [Test]
        [Description("A survey-capable fleet finds the nearest un-surveyed grav anomaly in its own system (the start " +
                     "system spawns ~30), and the order validates. This is the core selection the survey dispatch reads.")]
        public void SurveyNearestAnomaly_FindsAnUnsurveyedAnomaly_ForASurveyCapableFleet()
        {
            var s = TestScenario.CreateWithColony();
            var fleet = MakeSurveyFleet(s, "Pathfinder");

            var action = ServeyAnomalyAction.CreateCommand(s.Faction.Id, fleet);

            var target = action.FindNearestUnsurveyedAnomaly();
            Log(target == null ? "no anomaly found" : $"nearest anomaly = entity #{target.Id}");
            Assert.That(target, Is.Not.Null, "the start system has un-surveyed grav anomalies to find");
            Assert.That(target.HasDataBlob<JPSurveyableDB>(), Is.True, "the found target is a grav-survey anomaly");
            Assert.That(target.GetDataBlob<JPSurveyableDB>().IsSurveyComplete(s.Faction.Id), Is.False,
                        "and it is genuinely UN-surveyed for the player faction");

            Assert.That(action.IsValidCommand(s.Game), Is.True,
                        "a survey-capable player fleet can issue the survey order");
        }

        [Test]
        [Description("A fleet that CANNOT survey (no JP-survey ability) is refused the order — the survey-capability gate.")]
        public void SurveyNearestAnomaly_IsInvalid_ForANonSurveyFleet()
        {
            var s = TestScenario.CreateWithColony();
            var plain = FleetFactory.Create(s.StartingSystem, s.Faction.Id, "Freighter");   // no survey ship

            var action = ServeyAnomalyAction.CreateCommand(s.Faction.Id, plain);
            Assert.That(action.IsValidCommand(s.Game), Is.False,
                        "a fleet with no grav-survey ability cannot survey an anomaly");
        }

        [Test]
        [Description("Execute on a real survey fleet dispatches (warp + JPSurveyOrder) without throwing and completes so " +
                     "it leaves the order lane; a bare/unconstructed instance is still a safe no-op completion (A4 shell).")]
        public void SurveyNearestAnomaly_Execute_DispatchesWithoutThrowing_AndCompletes()
        {
            var s = TestScenario.CreateWithColony();
            var fleet = MakeSurveyFleet(s, "Scout");
            var action = ServeyAnomalyAction.CreateCommand(s.Faction.Id, fleet);

            Assert.That(() => action.Execute(s.Game.TimePulse.GameGlobalDateTime), Throws.Nothing,
                        "dispatching the warp + survey orders never throws");
            Assert.That(action.IsFinished(), Is.True, "the dispatcher completes so it clears the lane");

            // The A4 safety property still holds for a bare instance (no commanding fleet → nothing to survey → completes).
            var bare = new ServeyAnomalyAction();
            Assert.That(() => bare.Execute(DateTime.MinValue), Throws.Nothing, "bare Execute is a safe no-op");
            Assert.That(bare.IsFinished(), Is.True, "the no-op survey completes rather than lingering");
        }
    }
}
