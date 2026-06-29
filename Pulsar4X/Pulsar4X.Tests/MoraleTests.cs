using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pulsar4X.Colonies;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// M1 gauge for the morale/population loop (docs/MORALE-AND-POPULATION-DESIGN.md): morale is the
    /// level-control valve on the population "tank". These prove the pure morale math (deterministic) and
    /// that the real starting colony is born with a MoraleDB that sits at neutral on a hospitable world.
    /// </summary>
    [TestFixture]
    public class MoraleTests
    {
        [Test]
        [Description("A hospitable, uncrowded world sits at neutral morale; hostility and crowding each drag it down.")]
        public void ComputeMorale_ConditionsAndCrowding_DragMoraleDown()
        {
            var factors = new Dictionary<string, double>();

            // Hospitable (native, cost 0), uncrowded → exactly neutral.
            double hospitable = ColonyMoraleDB.ComputeMorale(0.0, 0.0, factors);
            Assert.That(hospitable, Is.EqualTo(ColonyMoraleDB.Neutral), "a native, uncrowded world should be neutral");
            Assert.That(factors["conditions"], Is.EqualTo(0.0));
            Assert.That(factors["crowding"], Is.EqualTo(0.0));

            // Hostile world (cost 2.0) → conditions penalty drops morale below neutral.
            double hostile = ColonyMoraleDB.ComputeMorale(2.0, 0.0, null);
            Assert.That(hostile, Is.LessThan(ColonyMoraleDB.Neutral), "a hostile world should depress morale");

            // Overcrowded (over capacity) on top of mild hostility → lower still.
            double crowded = ColonyMoraleDB.ComputeMorale(1.0, 1.2, null);
            double notCrowded = ColonyMoraleDB.ComputeMorale(1.0, 0.5, null);
            Assert.That(crowded, Is.LessThan(notCrowded), "overcrowding should drag morale below the same world uncrowded");
        }

        [Test]
        [Description("Crowding only bites past the threshold, and the conditions penalty is capped.")]
        public void ComputeMorale_ThresholdAndCaps()
        {
            // Below the crowding threshold there is no crowding penalty.
            double underThreshold = ColonyMoraleDB.ComputeMorale(0.0, ColonyMoraleDB.CrowdingThreshold - 0.1, null);
            Assert.That(underThreshold, Is.EqualTo(ColonyMoraleDB.Neutral));

            // A brutal world can't alone push morale below (Neutral - MaxConditionsPenalty).
            double brutal = ColonyMoraleDB.ComputeMorale(999.0, 0.0, null);
            Assert.That(brutal, Is.EqualTo(ColonyMoraleDB.Neutral - ColonyMoraleDB.MaxConditionsPenalty).Within(0.001));
        }

        [Test]
        [Description("Migration: neutral = no flow; below neutral = emigration; above = immigration; clamped at the ends.")]
        public void MigrationRate_SignAndBounds()
        {
            Assert.That(ColonyMoraleDB.MigrationRate(ColonyMoraleDB.Neutral), Is.EqualTo(0.0), "neutral morale = no migration");
            Assert.That(ColonyMoraleDB.MigrationRate(0.0), Is.EqualTo(-ColonyMoraleDB.MaxMigrationRate).Within(1e-9), "rock-bottom = max emigration");
            Assert.That(ColonyMoraleDB.MigrationRate(100.0), Is.EqualTo(ColonyMoraleDB.MaxMigrationRate).Within(1e-9), "max morale = max immigration");
            Assert.That(ColonyMoraleDB.MigrationRate(25.0), Is.LessThan(0.0), "below neutral = emigration");
            Assert.That(ColonyMoraleDB.MigrationRate(75.0), Is.GreaterThan(0.0), "above neutral = immigration");
        }

        [Test]
        [Description("M2: full employment buffs morale, unemployment debuffs it, and no job data is neutral.")]
        public void ComputeMorale_Employment_TwoSided()
        {
            // No job data (negative sentinel) == the M1 overload baseline.
            double noData = ColonyMoraleDB.ComputeMorale(0.0, 0.0, -1.0, 0.0, null);
            Assert.That(noData, Is.EqualTo(ColonyMoraleDB.Neutral), "no job data should be neutral, not penalized");

            // Full employment (jobs >= pop) is a buff above the no-data baseline.
            double fullEmployment = ColonyMoraleDB.ComputeMorale(0.0, 0.0, 1.0, 0.0, null);
            Assert.That(fullEmployment, Is.EqualTo(ColonyMoraleDB.Neutral + ColonyMoraleDB.MaxEmploymentBonus).Within(0.001));
            Assert.That(fullEmployment, Is.GreaterThan(noData));

            // Total unemployment (ratio 0) is the full debuff below baseline.
            double totalUnemployment = ColonyMoraleDB.ComputeMorale(0.0, 0.0, 0.0, 0.0, null);
            Assert.That(totalUnemployment, Is.EqualTo(ColonyMoraleDB.Neutral - ColonyMoraleDB.MaxUnemploymentPenalty).Within(0.001));
            Assert.That(totalUnemployment, Is.LessThan(noData));

            // Half employment sits between.
            double half = ColonyMoraleDB.ComputeMorale(0.0, 0.0, 0.5, 0.0, null);
            Assert.That(half, Is.GreaterThan(totalUnemployment).And.LessThan(fullEmployment));
        }

        [Test]
        [Description("M2: housing comfort is a capped positive morale bonus.")]
        public void ComputeMorale_Comfort_BonusCapped()
        {
            var factors = new Dictionary<string, double>();
            double withComfort = ColonyMoraleDB.ComputeMorale(0.0, 0.0, -1.0, 10.0, factors);
            Assert.That(withComfort, Is.EqualTo(ColonyMoraleDB.Neutral + 10.0).Within(0.001));
            Assert.That(factors["comfort"], Is.EqualTo(10.0).Within(0.001));

            // Capped — a lavish comfort value can't exceed the cap.
            double capped = ColonyMoraleDB.ComputeMorale(0.0, 0.0, -1.0, 9999.0, null);
            Assert.That(capped, Is.EqualTo(ColonyMoraleDB.Neutral + ColonyMoraleDB.MaxComfortBonus).Within(0.001));
        }

        [Test]
        [Description("M5: power and food shortages drag morale down (food bites harder); neutral when no shortage.")]
        public void ComputeMorale_PowerAndFoodShortage_DragMoraleDown()
        {
            var none = new MoraleInputs { EmploymentRatio = -1.0 }; // all else 0 = the neutral case
            double baseM = ColonyMoraleDB.ComputeMorale(none, null);
            Assert.That(baseM, Is.EqualTo(ColonyMoraleDB.Neutral), "no shortages = neutral");

            var brownout = new MoraleInputs { EmploymentRatio = -1.0, PowerShortage = 0.5 };
            var famine = new MoraleInputs { EmploymentRatio = -1.0, FoodShortage = 0.5 };
            Assert.That(ColonyMoraleDB.ComputeMorale(brownout, null), Is.LessThan(baseM), "power shortage lowers morale");
            Assert.That(ColonyMoraleDB.ComputeMorale(famine, null), Is.LessThan(baseM), "food shortage lowers morale");
            Assert.That(ColonyMoraleDB.ComputeMorale(famine, null), Is.LessThan(ColonyMoraleDB.ComputeMorale(brownout, null)),
                "starvation should bite harder than a brownout at the same shortage fraction");

            var total = new MoraleInputs { EmploymentRatio = -1.0, PowerShortage = 1.0, FoodShortage = 1.0 };
            double expected = Math.Max(0.0, ColonyMoraleDB.Neutral - ColonyMoraleDB.MaxPowerShortagePenalty - ColonyMoraleDB.MaxFoodShortagePenalty);
            Assert.That(ColonyMoraleDB.ComputeMorale(total, null), Is.EqualTo(expected).Within(0.001));
        }

        [Test]
        [Description("The positional ComputeMorale overload matches the canonical MoraleInputs path (back-compat after the refactor).")]
        public void ComputeMorale_PositionalOverload_MatchesStruct()
        {
            double positional = ColonyMoraleDB.ComputeMorale(1.0, 0.2, 0.5, 5.0, 0.2, null);
            var inp = new MoraleInputs { WorstColonyCost = 1.0, CrowdingRatio = 0.2, EmploymentRatio = 0.5, Comfort = 5.0, TaxRate = 0.2 };
            double viaStruct = ColonyMoraleDB.ComputeMorale(inp, null);
            Assert.That(positional, Is.EqualTo(viaStruct).Within(0.0001));
        }

        [Test]
        [Description("MoraleDB clones deeply (survives entity transfer / save-load).")]
        public void ColonyMoraleDB_ClonesDeeply()
        {
            var original = new ColonyMoraleDB { Morale = 33.0 };
            original.Factors["conditions"] = -17.0;

            var clone = (ColonyMoraleDB)original.Clone();
            Assert.That(clone.Morale, Is.EqualTo(33.0));
            Assert.That(clone.Factors["conditions"], Is.EqualTo(-17.0));

            clone.Factors["conditions"] = 0.0;
            Assert.That(original.Factors["conditions"], Is.EqualTo(-17.0), "Factors dict was shared, not cloned");
        }

        [Test]
        [Description("The real starting colony is born with a MoraleDB and holds neutral morale on a hospitable homeworld (no emigration).")]
        public void StartingColony_HasMorale_NeutralOnHomeworld()
        {
            var s = TestScenario.CreateWithColony();

            Assert.That(s.Colony.HasDataBlob<ColonyMoraleDB>(), Is.True, "a colony should be born with a ColonyMoraleDB");

            long startPop = s.Colony.GetDataBlob<ColonyInfoDB>().Population.Values.Sum();
            s.AdvanceTime(TimeSpan.FromDays(60)); // two monthly population ticks

            var morale = s.Colony.GetDataBlob<ColonyMoraleDB>();
            Assert.That(morale.Morale, Is.InRange(0.0, 100.0));
            // A homeworld is native (ColonyCost 0) and not support-capped, so morale sits at neutral and there
            // is no morale-driven emigration — population should not have shrunk from migration.
            Assert.That(morale.Morale, Is.GreaterThanOrEqualTo(ColonyMoraleDB.Neutral),
                "a hospitable homeworld should not be below neutral in M1");
            long endPop = s.Colony.GetDataBlob<ColonyInfoDB>().Population.Values.Sum();
            Assert.That(endPop, Is.GreaterThanOrEqualTo(startPop), "neutral/high morale should not bleed population");
        }
    }
}
