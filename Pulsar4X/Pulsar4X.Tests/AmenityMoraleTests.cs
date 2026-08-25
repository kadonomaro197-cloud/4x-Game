using System.Collections.Generic;
using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Components;   // ComponentInstance, ComponentDesign
using Pulsar4X.Datablobs;    // ComponentInstancesDB
using Pulsar4X.Colonies;     // ColonyMoraleDB, MoraleInputs, PopulationProcessor, AmenityAtbDB
using Pulsar4X.Extensions;   // GetTotalAmenity

namespace Pulsar4X.Tests
{
    /// <summary>
    /// The RECREATION civic dial — a colony's RECREATION CENTERS (components carrying <see cref="AmenityAtbDB"/>) lift its
    /// MORALE (docs/Actual HTMLs Of designers/civicderived.html — the Recreation option, a NEW morale consumer, not one
    /// of the original six inputs, the SIBLING of the Medical health term). The BUILD sibling of the Medical→morale and
    /// Security→legitimacy dials and the food/employment terms: a NEW component attribute summed into a NEW, flag-gated
    /// input on the well-tested morale formula. Flag OFF (the engine default) → byte-identical (no colony ships a
    /// recreation center, and the term adds no factor at 0). Cradle-to-grave: designed/built → lifts morale → destroyed
    /// (bombardment) drops it.
    /// </summary>
    [TestFixture]
    public class AmenityMoraleTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[amenity] " + m);

        private bool _savedAmenity, _savedEmployment;

        [SetUp]
        public void SaveFlags()
        {
            _savedAmenity = PopulationProcessor.EnableAmenityMorale;
            _savedEmployment = PopulationProcessor.EnableEmploymentMorale;
            // Isolate the amenity term: keep employment OFF so a recreation center's operating crew can't shift the
            // employment term.
            PopulationProcessor.EnableEmploymentMorale = false;
        }

        [TearDown]
        public void RestoreFlags()
        {
            PopulationProcessor.EnableAmenityMorale = _savedAmenity;
            PopulationProcessor.EnableEmploymentMorale = _savedEmployment;
        }

        [Test]
        [Description("THE PURE MATH: ColonyMoraleDB.ComputeMorale's amenity term. 0 amenity (the default / flag-off / a colony with no recreation) adds NO factor and no change — byte-identical. A positive amenity rating adds itself to morale, capped at MaxAmenityBonus, so recreation lifts a colony but leisure alone can't make a miserable world happy.")]
        public void AmenityTerm_PureMath_NeutralAtZero_CappedAndAdditive()
        {
            var factors = new Dictionary<string, double>();

            // 0 amenity → no factor recorded, no morale change (the byte-identical default).
            var inp = new MoraleInputs { EmploymentRatio = -1.0 };   // -1 = "no job data" neutral, everything else 0
            double baseMorale = ColonyMoraleDB.ComputeMorale(inp, factors);
            Assert.That(factors.ContainsKey("amenity"), Is.False, "0 amenity → no amenity factor (byte-identical)");

            // A positive amenity rating adds itself to morale (under the cap) and records the factor.
            inp.AmenityStrength = 10.0;
            double withAmenity = ColonyMoraleDB.ComputeMorale(inp, factors);
            Assert.That(factors["amenity"], Is.EqualTo(10.0).Within(1e-9), "the amenity factor = the amenity rating (under the cap)");
            Assert.That(withAmenity - baseMorale, Is.EqualTo(10.0).Within(1e-9), "amenity adds its points to morale");

            // Capped at MaxAmenityBonus — leisure alone can't manufacture happiness.
            inp.AmenityStrength = 100.0;
            ColonyMoraleDB.ComputeMorale(inp, factors);
            Assert.That(factors["amenity"], Is.EqualTo(ColonyMoraleDB.MaxAmenityBonus).Within(1e-9),
                "the amenity bonus is capped — recreation lifts a colony, it doesn't manufacture happiness");
        }

        [Test]
        [Description("CRADLE-TO-GRAVE end-to-end through the real morale reader: the recreation center is a registered buildable start design (six-point registration); installing one makes GetTotalAmenity read its amenity rating; with the flag ON the colony's morale rises by that amenity (capped); with the flag OFF it's byte-identical (the center present but its amenity unread). THE GRAVE RUNG: destroying the center drops the amenity to 0 and morale falls back.")]
        public void RecreationCenter_LiftsMorale_FlagGated_CradleToGrave()
        {
            var s = TestScenario.CreateWithColony();
            var factionInfo = s.Faction.GetDataBlob<FactionInfoDB>();
            var comps = s.Colony.GetDataBlob<ComponentInstancesDB>();

            // Registration (L6 six-point): the recreation center is a registered buildable design on the start faction.
            Assert.That(factionInfo.ComponentDesigns.ContainsKey("default-design-recreation-center"), Is.True,
                "the recreation center is a registered buildable start design");
            var centerDesign = factionInfo.ComponentDesigns["default-design-recreation-center"];

            // The start colony ships NO recreation center → zero amenity (neutral).
            Assert.That(comps.GetTotalAmenity(), Is.EqualTo(0.0), "the start colony ships no recreation center → no amenity");

            // Install a recreation center → GetTotalAmenity reads its rating (10) at full health.
            var instance = new ComponentInstance(centerDesign);
            s.Colony.AddComponent(instance);
            Assert.That(comps.GetTotalAmenity(), Is.EqualTo(10.0).Within(0.01), "the installed recreation center provides its amenity rating");

            // Flag OFF → the center's amenity is UNREAD → byte-identical morale.
            PopulationProcessor.EnableAmenityMorale = false;
            double moraleAmenityUnread = PopulationProcessor.ComputeCurrentMorale(s.Colony);
            Log($"morale (center installed, amenity unread) = {moraleAmenityUnread:0.0}");

            // Flag ON → the amenity lifts morale (the center is installed in BOTH reads, so the delta is the amenity term alone).
            PopulationProcessor.EnableAmenityMorale = true;
            double moraleAmused = PopulationProcessor.ComputeCurrentMorale(s.Colony);
            Log($"morale (center installed, amenity read) = {moraleAmused:0.0} (+{moraleAmused - moraleAmenityUnread:0.0})");
            Assert.That(moraleAmused, Is.GreaterThan(moraleAmenityUnread), "a colony with recreation reads higher morale");

            // THE GRAVE RUNG: destroy the recreation center (an orbital-bombardment installation-kill) → amenity drops to 0
            // → morale falls back to the no-leisure baseline. Recreation is a real, LOSABLE lift on morale.
            comps.RemoveComponentInstance(instance);
            Assert.That(comps.GetTotalAmenity(), Is.EqualTo(0.0), "destroyed recreation center → no amenity (the grave rung)");
            double moraleAfterLoss = PopulationProcessor.ComputeCurrentMorale(s.Colony);   // flag still on, but no center
            Assert.That(moraleAfterLoss, Is.EqualTo(moraleAmenityUnread).Within(0.01),
                "with the recreation center gone, morale falls back to the no-leisure baseline");
        }
    }
}
