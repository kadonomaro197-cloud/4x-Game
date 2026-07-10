using System.Collections.Generic;
using NUnit.Framework;
using GameEngine.People;
using Pulsar4X.People;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Gauge for the durable-seat fix (Command foundation, dossier ⚙10). Before the fix,
    /// AdminSpaceProcessor.CalcEntityAdminSpace rebuilt the seat list from scratch on every pass, so the
    /// next processor tick silently un-seated every administrator — nothing downstream could hold an
    /// assignment. ReconcileSeats now (1) carries a seated commander across a recalc, matched by component
    /// name (the same key AssignAdministratorOrder uses), (2) adds a fresh empty seat for a new component,
    /// and (3) drops a seat whose component was removed while clearing its occupant's assignment.
    /// Pure function → no game scaffolding required.
    /// </summary>
    [TestFixture]
    public class AdminSpaceSeatReconcileTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[admin-seats] " + m);

        [Test]
        [Description("A seated administrator survives a seat recalc (the durable-seat fix) — the bug this slice closes.")]
        public void ReconcileSeats_PreservesSeatedCommander_AcrossRecalc()
        {
            // A colony HQ seat with a commander sitting in it.
            var seat = new AdminSpaceAbilityState(AdminLevel.Colony, "admin-complex");
            var commander = new CommanderDB();
            seat.CommanderID = 42;
            seat.Commander = commander;
            commander.AssignedTo = 7; // assigned to some admin entity

            var previous = new List<AdminSpaceAbilityState> { seat };
            // The same component is still installed -> a recalc must keep the same seat + occupant.
            var current = new List<(AdminLevel level, string name)> { (AdminLevel.Colony, "admin-complex") };

            var result = AdminSpaceProcessor.ReconcileSeats(previous, current);

            Assert.That(result, Has.Count.EqualTo(1), "one component -> one seat");
            Assert.That(ReferenceEquals(result[0], seat), Is.True, "the existing seat (with its commander) is reused, not rebuilt");
            Assert.That(result[0].CommanderID, Is.EqualTo(42), "the administrator is still seated after recalc");
            Assert.That(commander.AssignedTo, Is.EqualTo(7), "a still-seated commander keeps its assignment");
            Log("seated commander survived the recalc");
        }

        [Test]
        [Description("A newly-installed admin component adds a fresh empty seat.")]
        public void ReconcileSeats_AddsSeat_ForNewComponent()
        {
            var previous = new List<AdminSpaceAbilityState>();
            var current = new List<(AdminLevel level, string name)> { (AdminLevel.Fleet, "ship-command") };

            var result = AdminSpaceProcessor.ReconcileSeats(previous, current);

            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0].ComponentName, Is.EqualTo("ship-command"));
            Assert.That(result[0].SeatType, Is.EqualTo(AdminLevel.Fleet));
            Assert.That(result[0].CommanderID, Is.EqualTo(-1), "a fresh seat is empty");
            Log("new component produced a fresh empty seat");
        }

        [Test]
        [Description("Removing an admin component drops its seat and frees its occupant (no dangling assignment).")]
        public void ReconcileSeats_DropsSeat_AndUnassignsCommander_WhenComponentRemoved()
        {
            var seat = new AdminSpaceAbilityState(AdminLevel.Colony, "admin-complex");
            var commander = new CommanderDB();
            seat.CommanderID = 42;
            seat.Commander = commander;
            commander.AssignedTo = 7;

            var previous = new List<AdminSpaceAbilityState> { seat };
            // The component is gone (empty current) -> the seat drops and the commander is freed.
            var current = new List<(AdminLevel level, string name)>();

            var result = AdminSpaceProcessor.ReconcileSeats(previous, current);

            Assert.That(result, Is.Empty, "no components -> no seats");
            Assert.That(commander.AssignedTo, Is.EqualTo(-1), "the displaced administrator is unassigned (no dangling assignment)");
            Log("removed component dropped its seat and freed the commander");
        }

        [Test]
        [Description("Two same-named seats are both carried once each (no double-carry, no cross-assignment).")]
        public void ReconcileSeats_HandlesDuplicateComponentNames()
        {
            var seatA = new AdminSpaceAbilityState(AdminLevel.Colony, "admin-complex") { CommanderID = 1 };
            var seatB = new AdminSpaceAbilityState(AdminLevel.Colony, "admin-complex") { CommanderID = 2 };
            var previous = new List<AdminSpaceAbilityState> { seatA, seatB };
            var current = new List<(AdminLevel level, string name)>
            {
                (AdminLevel.Colony, "admin-complex"),
                (AdminLevel.Colony, "admin-complex")
            };

            var result = AdminSpaceProcessor.ReconcileSeats(previous, current);

            Assert.That(result, Has.Count.EqualTo(2));
            Assert.That(result, Does.Contain(seatA));
            Assert.That(result, Does.Contain(seatB));
            Log("duplicate-named seats each carried exactly once");
        }
    }
}
