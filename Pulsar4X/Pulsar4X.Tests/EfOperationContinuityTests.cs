using System;
using System.Linq;
using NUnit.Framework;
using Pulsar4X.Datablobs;    // OrderableDB
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Fleets;       // FleetDB, FleetFactory
using Pulsar4X.GroundCombat; // GroundForcesDB, GroundUnit, GroundUnitType
using Pulsar4X.Movement;     // WarpMovingDB, MoveToSystemBodyOrder

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Operation Earthfall P3.4 gauge (findings/A3-objective-flip.md §D commitment gap + seam 5 — "never orphan an
    /// invasion"). Two guards, tested by driving the pure decision + the resolvers DIRECTLY (no sim advance):
    ///
    /// (a) A WINNING in-flight Conquer SURVIVES a transient internal wobble instead of being hijacked to Defend
    ///     (the phantom-rebellion / hostile-world morale-trough A3 documented). The protection flows THROUGH the
    ///     transition engine's release logic (<see cref="ObjectiveTransition.ShouldProtectInFlightConquest"/> →
    ///     <see cref="ObjectiveTransition.ShouldReplan"/>/<see cref="ObjectiveTransition.Advance"/> suppress the
    ///     "more-urgent tier preempts" path), plus the two entity reads that feed it
    ///     (<see cref="NeedsLadder.HomelandInvaded"/> / <see cref="ConquerResolver.HasFleetInTransit"/>).
    /// (b) A GENUINE flip to Defend actively RECALLS an in-flight offensive fleet: <see cref="DefendResolver"/>
    ///     emits a return <see cref="MoveToSystemBodyOrder"/> to the home colony body, instead of only re-posturing.
    /// </summary>
    [TestFixture]
    public class EfOperationContinuityTests
    {
        private static readonly DateTime T0 = new DateTime(2050, 1, 1);

        // An internal-only Survive shock (what A3 was): a rebellion, no lost war, no invaded homeland.
        private const CrisisTrigger InternalShock = CrisisTrigger.Rebellion;

        // ── (a) the PURE protection decision — each gate bites ─────────────────────────────────────────────────────

        [Test]
        [Description("(a) ShouldProtectInFlightConquest protects ONLY a winning in-flight Conquer facing a transient "
                   + "internal Survive wobble — every one of the six gates individually vetoes the protection.")]
        public void ShouldProtectInFlightConquest_EachGateBites()
        {
            // All gates met → protected.
            Assert.That(ObjectiveTransition.ShouldProtectInFlightConquest(
                StrategicObjective.Conquer, NeedTier.Survive, atWarAndWinning: true,
                InternalShock, homelandInvaded: false, hasInFlightStrikeFleet: true), Is.True,
                "a winning in-flight Conquer with a purely internal Survive wobble is protected");

            // Gate 1 — not committed to Conquer.
            Assert.That(ObjectiveTransition.ShouldProtectInFlightConquest(
                StrategicObjective.Defend, NeedTier.Survive, true, InternalShock, false, true), Is.False,
                "only an in-flight CONQUER is protected");

            // Gate 2 — no strike fleet en route.
            Assert.That(ObjectiveTransition.ShouldProtectInFlightConquest(
                StrategicObjective.Conquer, NeedTier.Survive, true, InternalShock, false, false), Is.False,
                "no invasion in flight → nothing to orphan → no protection");

            // Gate 3 — not winning the war.
            Assert.That(ObjectiveTransition.ShouldProtectInFlightConquest(
                StrategicObjective.Conquer, NeedTier.Survive, false, InternalShock, false, true), Is.False,
                "only a WINNING war is worth pressing on through a wobble");

            // Gate 4 — the downgrade isn't to the Survive floor.
            Assert.That(ObjectiveTransition.ShouldProtectInFlightConquest(
                StrategicObjective.Conquer, NeedTier.Stabilize, true, InternalShock, false, true), Is.False,
                "protection is only against a Survive-floor downgrade");

            // Gate 5 — the homeland is physically invaded (a genuine external crisis).
            Assert.That(ObjectiveTransition.ShouldProtectInFlightConquest(
                StrategicObjective.Conquer, NeedTier.Survive, true, InternalShock, true, true), Is.False,
                "an enemy on home soil is a GENUINE crisis → not protected (recall the fleets)");

            // Gate 6a — the war is being lost (a genuine external crisis), even with an internal shock also present.
            Assert.That(ObjectiveTransition.ShouldProtectInFlightConquest(
                StrategicObjective.Conquer, NeedTier.Survive, true,
                CrisisTrigger.Rebellion | CrisisTrigger.LosingWar, false, true), Is.False,
                "a lost war is not transient → not protected");

            // Gate 6b — the Survive read isn't from any internal shock.
            Assert.That(ObjectiveTransition.ShouldProtectInFlightConquest(
                StrategicObjective.Conquer, NeedTier.Survive, true, CrisisTrigger.None, false, true), Is.False,
                "no internal shock recorded → nothing transient to protect against");
        }

        [Test]
        [Description("(a) protectCommit suppresses ShouldReplan's 'more-urgent tier preempts' path (the winning "
                   + "invasion holds through the transient Survive wobble) but NOT a genuine expiry.")]
        public void ProtectedCommit_HoldsThroughTransientWobble_ButNotExpiry()
        {
            var committedUntil = T0.AddDays(180);

            // A Conquer committed at the Ambition tier; a transient Survive (0) proposal.
            Assert.That(ObjectiveTransition.ShouldReplan(NeedTier.Ambition, committedUntil, NeedTier.Survive, T0.AddDays(30)),
                Is.True, "WITHOUT protection a Survive emergency preempts the higher-tier Conquer commit");

            Assert.That(ObjectiveTransition.ShouldReplan(NeedTier.Ambition, committedUntil, NeedTier.Survive, T0.AddDays(30),
                protectCommit: true), Is.False,
                "WITH protection the in-flight conquest holds through the transient Survive wobble");

            // Protection stops the transient hijack, not a genuine expiry.
            Assert.That(ObjectiveTransition.ShouldReplan(NeedTier.Ambition, committedUntil, NeedTier.Survive, T0.AddDays(200),
                protectCommit: true), Is.True,
                "a protected commit still re-plans once its dwell genuinely runs out");
        }

        [Test]
        [Description("(a) Advance: a protected transient wobble HOLDS the committed Conquer; the same wobble WITHOUT "
                   + "protection preempts to Defend (the genuine-crisis / recall path).")]
        public void Advance_ProtectedConquerHolds_UnprotectedPreemptsToDefend()
        {
            var obj = new StrategicObjectiveDB();
            var commit = TimeSpan.FromDays(180);

            // Commit Conquer at the Ambition tier.
            Assert.That(ObjectiveTransition.Advance(obj, NeedTier.Ambition, StrategicObjective.Conquer, -1, T0, commit),
                Is.True, "the fresh objective adopts Conquer");
            Assert.That(obj.Objective, Is.EqualTo(StrategicObjective.Conquer));

            // A transient internal Survive wobble proposes Defend — WITH protection it is HELD.
            Assert.That(ObjectiveTransition.Advance(obj, NeedTier.Survive, StrategicObjective.Defend, -1, T0.AddDays(30),
                commit, InternalShock, protectCommit: true), Is.False, "protected → held");
            Assert.That(obj.Objective, Is.EqualTo(StrategicObjective.Conquer), "the winning conquest is not orphaned");

            // The SAME wobble WITHOUT protection preempts to Defend (a genuine crisis / the recall path).
            Assert.That(ObjectiveTransition.Advance(obj, NeedTier.Survive, StrategicObjective.Defend, -1, T0.AddDays(31),
                commit, InternalShock, protectCommit: false), Is.True, "unprotected → preempts");
            Assert.That(obj.Objective, Is.EqualTo(StrategicObjective.Defend),
                "a genuine Defend switch takes over (DefendResolver then recalls the fleets)");
        }

        // ── (a) the two ENTITY reads that feed the protection decision ─────────────────────────────────────────────

        [Test]
        [Description("(a) HomelandInvaded is true ONLY when a foreign (non-owned, non-neutral) ground unit stands on "
                   + "one of the faction's own colony worlds.")]
        public void HomelandInvaded_TrueOnlyWithAForeignUnitOnAHomeWorld()
        {
            var s = TestScenario.CreateWithColony();
            var reds = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);

            Assert.That(NeedsLadder.HomelandInvaded(s.Faction), Is.False,
                "no foreign unit on the home world → not invaded (any own start garrison doesn't count)");

            // Land a foreign garrison on the home world's surface.
            if (!s.StartingBody.TryGetDataBlob<GroundForcesDB>(out var forces))
            {
                forces = new GroundForcesDB();
                s.StartingBody.SetDataBlob(forces);
            }
            forces.Units.Add(new GroundUnit
            {
                UnitId = 99, Name = "Invaders", FactionOwnerID = reds.Id,
                UnitType = GroundUnitType.Infantry, Attack = 50, MaxHealth = 100, Health = 100, RegionIndex = 0,
            });

            Assert.That(NeedsLadder.HomelandInvaded(s.Faction), Is.True,
                "a foreign garrison on a home world reads as invaded — a genuine external crisis");
        }

        [Test]
        [Description("(a) HasFleetInTransit is true ONLY when the faction owns a fleet with a ship in warp transit.")]
        public void HasFleetInTransit_TrueOnlyWithAMovingOwnedFleet()
        {
            var s = TestScenario.CreateWithColony();

            Assert.That(ConquerResolver.HasFleetInTransit(s.Faction), Is.False,
                "the parked start fleets are not in warp transit");

            // A fleet with one ship in warp transit.
            var fleet = FleetFactory.Create(s.StartingSystem, s.Faction.Id, "Transit Fleet");
            AddTransitShip(s, fleet, s.StartingBody);   // any target — HasFleetInTransit only reads WarpMovingDB presence

            Assert.That(ConquerResolver.HasFleetInTransit(s.Faction), Is.True,
                "a fleet with a ship in warp transit reads in-transit (the 'invasion en route' signal)");
        }

        // ── (b) DefendResolver RECALLS an in-flight offensive fleet ────────────────────────────────────────────────

        [Test]
        [Description("(b) On Defend, an in-flight offensive fleet (a ship warping into foreign space) is RECALLED: the "
                   + "resolver decides RecallFleet and Execute emits a MoveToSystemBodyOrder to the home colony body.")]
        public void Defend_RecallsAnInFlightOffensiveFleet_ToHome()
        {
            var s = TestScenario.CreateWithColony();

            // A body that is NOT one of our colony worlds → warping toward it is an outbound sortie.
            var enemyBody = Entity.Create();
            s.StartingSystem.AddEntity(enemyBody);

            var fleet = FleetFactory.Create(s.StartingSystem, s.Faction.Id, "Strike Group");
            AddTransitShip(s, fleet, enemyBody);   // a ship warping AT the enemy world

            var state = FactionState.Snapshot(s.Faction);
            var action = new DefendResolver().Resolve(state, new StrategicObjectiveDB { Objective = StrategicObjective.Defend });

            Assert.That(action.Kind, Is.EqualTo("RecallFleet"),
                "an in-flight offensive fleet is recalled home before building/posturing");
            Assert.That(action.Detail, Does.Contain(s.StartingBody.Id.ToString()),
                "the recall targets the home colony body");

            // Execute emits the recall order onto the fleet's order queue.
            action.Execute();
            var queued = fleet.GetDataBlob<OrderableDB>().ActionList.OfType<MoveToSystemBodyOrder>().FirstOrDefault();
            Assert.That(queued, Is.Not.Null, "Execute emitted a MoveToSystemBodyOrder to the recalled fleet");
            Assert.That(queued.Target, Is.EqualTo(s.StartingBody), "the emitted recall order targets the home body");
        }

        [Test]
        [Description("(b) With no in-flight offensive fleet, Defend falls through to its build/posture rungs — it does "
                   + "NOT recall (byte-identical to the pre-P3.4 DefendResolver).")]
        public void Defend_NoOutboundFleet_FallsToBuildOrPosture()
        {
            var s = TestScenario.CreateWithColony();   // parked start fleets only — none in transit
            var state = FactionState.Snapshot(s.Faction);

            var action = new DefendResolver().Resolve(state, new StrategicObjectiveDB { Objective = StrategicObjective.Defend });

            Assert.That(action.Kind, Is.Not.EqualTo("RecallFleet"), "nothing to recall → no recall");
            Assert.That(action.Kind, Is.EqualTo("QueueWarship").Or.EqualTo("SetDefensivePosture"),
                "Defend still builds a warship or postures a fleet");
        }

        [Test]
        [Description("(b) A fleet already warping HOME (or between our own worlds) is NOT recalled — the guard that "
                   + "keeps the recall from being re-issued endlessly.")]
        public void Defend_DoesNotRecall_AFleetAlreadyHeadingHome()
        {
            var s = TestScenario.CreateWithColony();

            var fleet = FleetFactory.Create(s.StartingSystem, s.Faction.Id, "Returning Fleet");
            AddTransitShip(s, fleet, s.StartingBody);   // warping toward HOME — not outbound

            var state = FactionState.Snapshot(s.Faction);
            var action = new DefendResolver().Resolve(state, new StrategicObjectiveDB { Objective = StrategicObjective.Defend });

            Assert.That(action.Kind, Is.Not.EqualTo("RecallFleet"),
                "a fleet already heading home is not recalled again");
        }

        /// <summary>Give <paramref name="fleet"/> a child ship in warp transit toward <paramref name="target"/> — the
        /// 'in-flight' state the transit/outbound reads key on (a bare entity carrying only a <see cref="WarpMovingDB"/>
        /// whose <c>TargetEntity</c> is the destination; the reads only inspect that blob).</summary>
        private static Entity AddTransitShip(TestScenario s, Entity fleet, Entity target)
        {
            var ship = Entity.Create();
            s.StartingSystem.AddEntity(ship);
            ship.FactionOwnerID = s.Faction.Id;
            var warp = new WarpMovingDB { TargetEntity = target };
            ship.SetDataBlob(warp);
            fleet.GetDataBlob<FleetDB>().AddChild(ship);
            return ship;
        }
    }
}
