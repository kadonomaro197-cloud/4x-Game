using System;
using System.Linq;
using NUnit.Framework;
using Pulsar4X.Factions;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// F-C3c gauge (docs/ESPIONAGE-AND-INTELLIGENCE-DESIGN.md): the covert-action catalog is complete and sane —
    /// every action has exactly one definition, every detection risk is a valid probability, and each carries a
    /// description. Pure data → byte-identical.
    /// </summary>
    [TestFixture]
    public class CovertActionCatalogTests
    {
        [Test]
        [Description("Every CovertAction has exactly one catalog def; risks are in [0,1]; GatherIntel is the cheap baseline.")]
        public void Catalog_IsComplete_AndValid()
        {
            var actions = (CovertAction[])Enum.GetValues(typeof(CovertAction));

            foreach (var action in actions)
                Assert.That(CovertActionCatalog.All.Count(d => d.Action == action), Is.EqualTo(1),
                    $"{action} must have exactly one catalog definition");

            Assert.That(CovertActionCatalog.All.Length, Is.EqualTo(actions.Length),
                "no extra or duplicate defs");

            foreach (var def in CovertActionCatalog.All)
            {
                Assert.That(def.BaseDetectionRisk, Is.InRange(0.0, 1.0), $"{def.Action} risk is a probability");
                Assert.That(def.Description, Is.Not.Empty, $"{def.Action} has a description");
            }

            Assert.That(CovertActionCatalog.ByAction(CovertAction.GatherIntel).Action, Is.EqualTo(CovertAction.GatherIntel));
            // The loud plays cost more risk than the quiet baseline.
            Assert.That(CovertActionCatalog.ByAction(CovertAction.TurnOrAssassinate).BaseDetectionRisk,
                Is.GreaterThan(CovertActionCatalog.ByAction(CovertAction.GatherIntel).BaseDetectionRisk),
                "turn/assassinate is far riskier than gathering intel");
        }
    }
}
