using NUnit.Framework;
using GameEngine.People;
using Pulsar4X.People;
using Pulsar4X.People.Orders;
using Pulsar4X.DataStructures;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// AdminLevel-wire (OPERATION BLUEPRINT-TO-STEEL, developer ruling "wire it", 2026-08-18) — the v1 SPAN-OF-CONTROL
    /// cap. Until now the <see cref="AdminLevel"/> a command seat carries (`AdminSpaceAtb`/`AdminSpaceAbilityState.SeatType`)
    /// was a label NO rule read. This slice makes it a real gate: a broader command SCOPE demands a more SENIOR officer,
    /// so a green lieutenant can run a colony but only a senior officer can be seated in an empire-wide command post.
    ///
    /// <para>The cap is a PURE function (<see cref="AdminSpaceProcessor.CanOfficerHoldSeat"/>) that
    /// <see cref="AssignAdministratorOrder.IsValidCommand"/> reads behind a flag. These gauges pin the pure rule
    /// (required-rank rises monotonically with scope; routine posts are ungated) and the byte-identity guarantee (the
    /// flag defaults OFF, so no existing assignment is refused). The rank map is FLAGGED/tunable — start officers are
    /// rank 1–6 (`CommanderFactory`), so the default offset leaves colony-and-below open to anyone.</para>
    /// </summary>
    [TestFixture]
    public class AdminRankGateTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[admin-rank] " + m);

        [Test]
        [Description("The required rank RISES with the command scope, and routine posts (Ship…Colony) are ungated — "
                     + "AdminLevel is now READ by a real rule. Default offset 5: Colony 0, Planet 1, System 3, Empire 5.")]
        public void AdminRankRequired_RisesWithScope_RoutinePostsUngated()
        {
            int saved = AdminSpaceProcessor.AdminRankLevelOffset;
            try
            {
                AdminSpaceProcessor.AdminRankLevelOffset = 5;   // the default, pinned for the assertion
                Assert.That(AdminSpaceProcessor.AdminRankRequired(AdminLevel.Ship), Is.EqualTo(0));
                Assert.That(AdminSpaceProcessor.AdminRankRequired(AdminLevel.Fleet), Is.EqualTo(0), "routine posts ungated");
                Assert.That(AdminSpaceProcessor.AdminRankRequired(AdminLevel.Colony), Is.EqualTo(0), "a colony takes any officer");
                Assert.That(AdminSpaceProcessor.AdminRankRequired(AdminLevel.Planet), Is.EqualTo(1));
                Assert.That(AdminSpaceProcessor.AdminRankRequired(AdminLevel.System), Is.EqualTo(3));
                Assert.That(AdminSpaceProcessor.AdminRankRequired(AdminLevel.Empire), Is.EqualTo(5));
                // Monotonic — a broader scope never needs LESS rank.
                Assert.That(AdminSpaceProcessor.AdminRankRequired(AdminLevel.Empire),
                    Is.GreaterThan(AdminSpaceProcessor.AdminRankRequired(AdminLevel.Colony)),
                    "an empire command demands more seniority than a colony post");
                Log($"required rank — Colony={AdminSpaceProcessor.AdminRankRequired(AdminLevel.Colony)} "
                    + $"System={AdminSpaceProcessor.AdminRankRequired(AdminLevel.System)} "
                    + $"Empire={AdminSpaceProcessor.AdminRankRequired(AdminLevel.Empire)}");
            }
            finally { AdminSpaceProcessor.AdminRankLevelOffset = saved; }
        }

        [Test]
        [Description("The span-of-control cap: a junior officer runs routine posts but is BLOCKED from a strategic "
                     + "command; a senior officer clears it. A null officer holds nothing. (Pure — the rule the assign "
                     + "order reads.)")]
        public void CanOfficerHoldSeat_JuniorBlockedFromStrategicScope_SeniorAllowed()
        {
            int saved = AdminSpaceProcessor.AdminRankLevelOffset;
            try
            {
                AdminSpaceProcessor.AdminRankLevelOffset = 5;
                var junior = new CommanderDB("Ensign", 1, CommanderTypes.Navy);   // rank 1 — a green officer
                var senior = new CommanderDB("Admiral", 6, CommanderTypes.Navy);  // rank 6 — the start senior

                Assert.That(AdminSpaceProcessor.CanOfficerHoldSeat(junior, AdminLevel.Colony), Is.True,
                    "a green officer can run a colony (ungated)");
                Assert.That(AdminSpaceProcessor.CanOfficerHoldSeat(junior, AdminLevel.System), Is.False,
                    "rank 1 < System bar (3) — a junior can't command a whole system");
                Assert.That(AdminSpaceProcessor.CanOfficerHoldSeat(junior, AdminLevel.Empire), Is.False,
                    "nor the empire (needs rank 5, has 1)");
                Assert.That(AdminSpaceProcessor.CanOfficerHoldSeat(senior, AdminLevel.System), Is.True,
                    "the senior officer (rank 6) clears the system bar (3)");
                Assert.That(AdminSpaceProcessor.CanOfficerHoldSeat(senior, AdminLevel.Empire), Is.True,
                    "and the empire bar (5)");
                Assert.That(AdminSpaceProcessor.CanOfficerHoldSeat(null, AdminLevel.Ship), Is.False,
                    "a null officer holds nothing");
                Log($"junior(1): colony={AdminSpaceProcessor.CanOfficerHoldSeat(junior, AdminLevel.Colony)} "
                    + $"empire={AdminSpaceProcessor.CanOfficerHoldSeat(junior, AdminLevel.Empire)}; "
                    + $"senior(6): empire={AdminSpaceProcessor.CanOfficerHoldSeat(senior, AdminLevel.Empire)}");
            }
            finally { AdminSpaceProcessor.AdminRankLevelOffset = saved; }
        }

        [Test]
        [Description("Byte-identity: the span-of-control gate defaults OFF, so AssignAdministratorOrder refuses no "
                     + "assignment until the developer turns it on (after tuning the rank map to their officer scale).")]
        public void RankGate_DefaultsOff()
        {
            Assert.That(AssignAdministratorOrder.EnableAdminRankGate, Is.False,
                "the span-of-control rank gate must default OFF so every existing assignment is byte-identical");
        }
    }
}
