using System;
using System.Linq;
using NUnit.Framework;
using Pulsar4X.Colonies;
using Pulsar4X.Engine;
using Pulsar4X.Extensions;
using Pulsar4X.Factions;
using Pulsar4X.GeoSurveys;
using Pulsar4X.Industry;
using Pulsar4X.Ships;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Operation Earthfall D1.1 gauge (task #35 — the Kithrin survey chain was DEAD at three rungs, so a station-only
    /// expansionist NPC could never colonize). Proves the <see cref="ExpandResolver"/> now DRIVES the survey→found
    /// chain end-to-end through real engine paths:
    ///   (b) with an IDLE survey-capable ship owned, Expand emits a real <see cref="GeoSurveyOrder"/> at an unsurveyed
    ///       colonizeable world; drive the <see cref="GeoSurveyProcessor"/> to completion and the FOUND rung then
    ///       founds a colony there (the chain that was previously an Execute=null message); and
    ///   (c) with NO surveyor owned, the fallback rung queues ONE surveyor build on a free line.
    /// Resolver-driven (no sim advance) — the resolver is a pure decision (the order/build rides the Execute closure),
    /// so we gauge the DECISION + drive the survey by hand, mirroring the ExpandResolver/ConquerResolver test idiom.
    /// </summary>
    [TestFixture]
    public class EfKithrinSurveyChainTests
    {
        private static int TotalJobs(Entity colony)
        {
            var ind = colony.GetDataBlob<IndustryAbilityDB>();
            int n = 0;
            foreach (var line in ind.ProductionLines.Values) n += line.Jobs.Count;
            return n;
        }

        /// <summary>The first colonizeable, uncolonized, still-unsurveyed body in the home system (a real survey target).</summary>
        private static Entity FirstUnsurveyedColonizeable(TestScenario s, int factionId)
        {
            return s.StartingSystem.GetAllEntitiesWithDataBlob<ColonizeableDB>()
                .FirstOrDefault(b => b.IsValid && !b.IsOrHasColony().Item1
                    && b.TryGetDataBlob<GeoSurveyableDB>(out var g) && !g.IsSurveyComplete(factionId));
        }

        [Test]
        [Description("D1.1(b): with an idle surveyor owned, Expand emits a real GeoSurveyOrder; drive the GeoSurveyProcessor "
                   + "to completion and the FOUND rung then settles that world — the survey→found chain the Kithrin lacked.")]
        public void Expand_SurveysWithAnIdleSurveyor_ThenFoundsWhenSurveyComplete()
        {
            var s = TestScenario.CreateWithColony();
            int factionId = s.Faction.Id;
            var factionInfo = s.Faction.GetDataBlob<FactionInfoDB>();

            // A survey-capable ship owned by the faction (the Kithrin's Sable equivalent): spawn a real surveyor design
            // — its geo-surveyor component installs a GeoSurveyAbilityDB, exactly as the base-mod Sable does.
            var surveyorDesign = factionInfo.ShipDesigns.Values.FirstOrDefault(d => ExpandResolver.IsSurveyor(d));
            Assert.That(surveyorDesign, Is.Not.Null, "the start faction has a buildable survey ship design to field");
            var surveyor = ShipFactory.CreateShip(surveyorDesign, s.Faction, s.StartingBody, "Test Sable");
            Assert.That(surveyor.TryGetDataBlob<GeoSurveyAbilityDB>(out var ability) && ability.Speed > 0, Is.True,
                "the spawned surveyor carries a working GeoSurveyAbilityDB from its geo-surveyor component");

            // Home-system colonizeable worlds begin as fog (unsurveyed), so the FOUND rung has nothing yet — the survey
            // leg must run first.
            var targetBody = FirstUnsurveyedColonizeable(s, factionId);
            Assert.That(targetBody, Is.Not.Null, "Sol has an uncolonized colonizeable world (Mars/Mercury/Luna) awaiting survey");

            var state = FactionState.Snapshot(s.Faction);
            var action = new ExpandResolver().Resolve(state, new StrategicObjectiveDB { Objective = StrategicObjective.Expand });

            // (b) The decision: SURVEY (not the old Execute=null "survey leg pending"), carrying a real order to run.
            Assert.That(action.Kind, Is.EqualTo("Survey"),
                "with an idle surveyor owned and worlds awaiting survey, Expand decides to SURVEY");
            Assert.That(action.Execute, Is.Not.Null, "the Survey step carries the GeoSurveyOrder to run on Execute");

            // The emit path is real: executing it issues the GeoSurveyOrder through the order handler without throwing.
            Assert.DoesNotThrow(() => action.Execute(), "issuing the emitted GeoSurveyOrder runs the real order path");

            // Drive the survey to completion (task: "drive GeoSurveyProcessor to completion"). A high survey speed
            // finishes any base-mod body in one pass; loop-bounded for safety regardless of PointsRequired.
            ability.Speed = 1_000_000;   // gauge-only: swamp the body's PointsRequired so the survey completes
            var proc = new GeoSurveyProcessor(surveyor, targetBody);
            var geo = targetBody.GetDataBlob<GeoSurveyableDB>();
            for (int i = 0; i < 100 && !geo.IsSurveyComplete(factionId); i++)
                proc.ProcessEntity(surveyor, s.Game.TimePulse.GameGlobalDateTime);
            Assert.That(geo.IsSurveyComplete(factionId), Is.True, "the surveyor completed the geo-survey of the target world");

            // Next cycle: the FOUND rung now has a surveyed colonizeable world → it settles it (the payoff the chain unlocks).
            var afterState = FactionState.Snapshot(s.Faction);
            var afterAction = new ExpandResolver().Resolve(afterState, new StrategicObjectiveDB { Objective = StrategicObjective.Expand });
            Assert.That(afterAction.Kind, Is.EqualTo("Found"),
                "once a world is surveyed, Expand founds a colony there — the survey→found chain is closed");
            Assert.That(afterAction.Execute, Is.Not.Null, "the Found step carries the CreateColonyOrder to run on Execute");
        }

        [Test]
        [Description("D1.1(c): with NO surveyor owned and worlds awaiting survey, the fallback rung queues ONE surveyor "
                   + "build on a free line (pure decision until Execute; Execute queues the job).")]
        public void Expand_BuildsASurveyor_WhenNoneOwned()
        {
            var s = TestScenario.CreateWithColony();
            int factionId = s.Faction.Id;

            // Strip the surveyor the start Science Fleet gave us, so the "no surveyor owned" fallback rung is reachable.
            foreach (var sys in s.Game.Systems)
                foreach (var ship in sys.GetAllEntitiesWithDataBlob<ShipInfoDB>().ToList())
                    if (ship.IsValid && ship.FactionOwnerID == factionId && ship.HasDataBlob<GeoSurveyAbilityDB>())
                        ship.Destroy();

            var state = FactionState.Snapshot(s.Faction);
            Assert.That(ExpandResolver.FactionOwnsSurveyor(state), Is.False,
                "precondition: the faction now owns no surveyor (the start one was removed)");
            Assert.That(FirstUnsurveyedColonizeable(s, factionId), Is.Not.Null,
                "precondition: colonizeable worlds still await survey (the trigger for the survey leg)");

            int before = TotalJobs(s.Colony);
            var action = new ExpandResolver().Resolve(state, new StrategicObjectiveDB { Objective = StrategicObjective.Expand });

            Assert.That(action.Kind, Is.EqualTo("BuildSurveyor"),
                "no surveyor owned → the fallback rung builds one to open the frontier");
            Assert.That(TotalJobs(s.Colony), Is.EqualTo(before), "Resolve is a pure decision — nothing queued until Execute");

            action.Execute();
            Assert.That(TotalJobs(s.Colony), Is.GreaterThan(before), "Execute queues the surveyor build (+ any sub-jobs)");
        }

        [Test]
        [Description("Guard: while a surveyor is already in production, the fallback rung does NOT re-queue another "
                   + "(the already-queued guard), and no idle surveyor exists yet → survey pending (no action).")]
        public void Expand_DoesNotRequeueSurveyor_WhileOneIsBuilding()
        {
            var s = TestScenario.CreateWithColony();
            int factionId = s.Faction.Id;

            foreach (var sys in s.Game.Systems)
                foreach (var ship in sys.GetAllEntitiesWithDataBlob<ShipInfoDB>().ToList())
                    if (ship.IsValid && ship.FactionOwnerID == factionId && ship.HasDataBlob<GeoSurveyAbilityDB>())
                        ship.Destroy();

            // First cycle queues one surveyor build.
            var state1 = FactionState.Snapshot(s.Faction);
            var first = new ExpandResolver().Resolve(state1, new StrategicObjectiveDB { Objective = StrategicObjective.Expand });
            Assert.That(first.Kind, Is.EqualTo("BuildSurveyor"), "first cycle builds a surveyor");
            first.Execute();
            Assert.That(ExpandResolver.SurveyorInProduction(FactionState.Snapshot(s.Faction)), Is.True,
                "the surveyor is now on the slipway");

            // Second cycle: a surveyor is already building AND none is idle-owned yet → the rung must NOT queue another.
            int jobsAfterFirst = TotalJobs(s.Colony);
            var state2 = FactionState.Snapshot(s.Faction);
            var second = new ExpandResolver().Resolve(state2, new StrategicObjectiveDB { Objective = StrategicObjective.Expand });
            Assert.That(second.Kind, Is.Not.EqualTo("BuildSurveyor"),
                "the already-in-production guard stops a second surveyor being queued every cycle");
            Assert.That(second.Execute, Is.Null, "survey-pending is a no-op decision (no side effect) this cycle");
            Assert.That(TotalJobs(s.Colony), Is.EqualTo(jobsAfterFirst), "no extra job was queued by the second cycle's decision");
        }
    }
}
