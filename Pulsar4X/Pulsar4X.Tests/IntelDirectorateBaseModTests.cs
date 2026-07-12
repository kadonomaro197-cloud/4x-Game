using NUnit.Framework;
using Pulsar4X.Components;
using Pulsar4X.Engine;
using Pulsar4X.Factions;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Espionage E1 — the base-mod Intelligence Directorate, through the REAL data path. The starting colony now lists a
    /// buildable <c>default-design-intelligence-directorate</c>; this proves it loads onto the faction and binds its
    /// <see cref="IntelDirectorateAtb"/> from JSON via the ComponentDesigner (template → NCalc → atb, gotcha #10 — the
    /// six-point registration). It also proves the atb's install/uninstall correctly grows and withdraws the colony's
    /// <see cref="IntelDirectorateDB"/> capacity seat (the "build the gear / lose the gear" rung), summing across
    /// installs and dropping the blob when the last directorate is torn down. The client is CI-blind, so a mis-ordered
    /// <c>AtbConstrArgs</c>, a wrong AttributeType namespace, or a bad ctor fails HERE, not in a player's New Game.
    /// </summary>
    [TestFixture]
    public class IntelDirectorateBaseModTests
    {
        [Test]
        [Description("E1: the intelligence directorate loads onto the start faction and binds its IntelDirectorateAtb from JSON with the op-capacity + counter-intel dials.")]
        public void IntelDirectorate_LoadsFromJson_BindsItsAtb_WithCapacityDials()
        {
            var s = TestScenario.CreateWithColony();
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().IndustryDesigns;

            Assert.That(designs.ContainsKey("default-design-intelligence-directorate"), Is.True,
                "the intelligence directorate loads onto the faction — the six-point registration is wired (template in " +
                "StartingItems, design in ComponentDesigns, materials stocked)");

            var design = designs["default-design-intelligence-directorate"] as ComponentDesign;
            Assert.That(design, Is.Not.Null, "default-design-intelligence-directorate is a ComponentDesign");

            Assert.That(design.HasAttribute<IntelDirectorateAtb>(), Is.True,
                "the design binds an IntelDirectorateAtb — the AttributeType FQN resolved and the ctor args matched");

            var atb = design.GetAttribute<IntelDirectorateAtb>();
            TestContext.Progress.WriteLine(
                $"[intel-directorate] opCapacity={atb.OpCapacity} counterIntel={atb.CounterIntelRating}");

            Assert.That(atb.OpCapacity, Is.EqualTo(2), "op capacity bound from the template default");
            Assert.That(atb.CounterIntelRating, Is.EqualTo(20), "counter-intel rating bound from the template default");
        }

        [Test]
        [Description("E1: installing the directorate atb seeds/grows the colony's IntelDirectorateDB capacity seat; uninstalling withdraws it, dropping the blob when the last one goes (build/lose the gear).")]
        public void IntelDirectorateAtb_InstallGrows_UninstallWithdraws_TheCapacitySeat()
        {
            var s = TestScenario.CreateWithColony();
            var colony = s.Colony;

            Assert.That(colony.HasDataBlob<IntelDirectorateDB>(), Is.False,
                "a colony with no directorate built carries no spy-capacity seat (byte-identical / additive)");

            var atb = new IntelDirectorateAtb(opCapacity: 2, counterIntelRating: 20);

            // Build the first directorate — the seat appears with its numbers.
            atb.OnComponentInstallation(colony, null);
            Assert.That(colony.HasDataBlob<IntelDirectorateDB>(), Is.True, "installing seeds the capacity seat");
            var seat = colony.GetDataBlob<IntelDirectorateDB>();
            Assert.That(seat.OpCapacity, Is.EqualTo(2), "one directorate → 2 concurrent ops");
            Assert.That(seat.CounterIntelRating, Is.EqualTo(20), "one directorate → 20 counter-intel");

            // Build a second — capacity is ADDITIVE (more infrastructure buys more spy capacity).
            atb.OnComponentInstallation(colony, null);
            Assert.That(seat.OpCapacity, Is.EqualTo(4), "two directorates → 4 concurrent ops (summed)");
            Assert.That(seat.CounterIntelRating, Is.EqualTo(40), "two directorates → 40 counter-intel (summed)");

            // Tear one down — capacity withdraws but the network survives.
            atb.OnComponentUninstallation(colony, null);
            Assert.That(colony.HasDataBlob<IntelDirectorateDB>(), Is.True, "one directorate still standing → seat remains");
            Assert.That(seat.OpCapacity, Is.EqualTo(2), "losing one directorate halves the capacity");

            // Tear down the last — no network left (the grave rung).
            atb.OnComponentUninstallation(colony, null);
            Assert.That(colony.HasDataBlob<IntelDirectorateDB>(), Is.False,
                "the last directorate torn down leaves the colony with no spy network");
            TestContext.Progress.WriteLine("[intel-directorate] install→grow→withdraw→drop verified");
        }
    }
}
