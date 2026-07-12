using System.Linq;
using NUnit.Framework;
using Pulsar4X.People;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Gauge for foundation slice 3a — the additive half of the rung-4 competence wire (dossiers ⚙6/⚙10).
    /// CommanderBonuses.CombatMultiplier reads a commander's BonusesDB and returns the combat multiplier for a
    /// category (product of (1 + Value) over matching bonuses, 1.0 when none). Slice 3b folds this into the
    /// fleet auto-resolver's FirepowerMult/ToughnessMult. Pure function → no game scaffolding.
    /// </summary>
    [TestFixture]
    public class CommanderCombatBonusTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[commander-bonus] " + m);

        [Test]
        [Description("No bonuses -> identity multiplier (an un-skilled commander changes nothing).")]
        public void CombatMultiplier_NoBonuses_IsIdentity()
        {
            var bonuses = new BonusesDB();
            Assert.That(CommanderBonuses.CombatMultiplier(bonuses, BonusCategory.Firepower), Is.EqualTo(1.0));
            Assert.That(CommanderBonuses.CombatMultiplier(null, BonusCategory.Firepower), Is.EqualTo(1.0), "null is safe");
            Log("identity for no/absent bonuses");
        }

        [Test]
        [Description("A matching-category bonus applies as a fraction: Value 0.15 -> x1.15.")]
        public void CombatMultiplier_AppliesMatchingCategory_AsFraction()
        {
            var bonuses = new BonusesDB();
            bonuses.Bonuses.Add(new Bonus("Veteran Gunnery", 0.15, BonusType.Perentage, BonusCategory.Firepower));

            Assert.That(CommanderBonuses.CombatMultiplier(bonuses, BonusCategory.Firepower), Is.EqualTo(1.15).Within(1e-9));
            Log("a +15% firepower commander yields x1.15");
        }

        [Test]
        [Description("Bonuses in other categories don't leak (firepower skill doesn't raise toughness).")]
        public void CombatMultiplier_IgnoresOtherCategories()
        {
            var bonuses = new BonusesDB();
            bonuses.Bonuses.Add(new Bonus("Iron Hull", 0.2, BonusType.Perentage, BonusCategory.Toughness));

            Assert.That(CommanderBonuses.CombatMultiplier(bonuses, BonusCategory.Firepower), Is.EqualTo(1.0));
            Assert.That(CommanderBonuses.CombatMultiplier(bonuses, BonusCategory.Toughness), Is.EqualTo(1.2).Within(1e-9));
            Log("categories stay separate");
        }

        [Test]
        [Description("Multiple bonuses in a category stack multiplicatively (0.1 and 0.1 -> x1.21).")]
        public void CombatMultiplier_StacksMultipleBonuses()
        {
            var bonuses = new BonusesDB();
            bonuses.Bonuses.Add(new Bonus("Drill A", 0.1, BonusType.Perentage, BonusCategory.Firepower));
            bonuses.Bonuses.Add(new Bonus("Drill B", 0.1, BonusType.Perentage, BonusCategory.Firepower));

            Assert.That(CommanderBonuses.CombatMultiplier(bonuses, BonusCategory.Firepower), Is.EqualTo(1.21).Within(1e-9));
            Log("two +10% bonuses compound to x1.21");
        }

        // ── Slice 3c: the academy competence GENERATOR ────────────────────────────────────────

        [Test]
        [Description("A graduate's ExperienceCap becomes Firepower+Toughness bonuses, and no potential -> none (slice 3c).")]
        public void RollCombatCompetence_ScalesBonusesWithExperienceCap()
        {
            Assert.That(CommanderBonuses.RollCombatCompetence(0), Is.Empty, "a no-potential washout gets no combat bonus");
            Assert.That(CommanderBonuses.RollCombatCompetence(-5), Is.Empty, "negative cap is safe");

            var top = CommanderBonuses.RollCombatCompetence(200);
            Assert.That(top.Count, Is.EqualTo(2), "a top graduate gets a firepower and a toughness bonus");
            Assert.That(top.First(b => b.Category == BonusCategory.Firepower).Value,
                Is.EqualTo(CommanderBonuses.MaxCombatCompetenceBonus).Within(1e-9), "cap 200 -> full bonus");
            Assert.That(top.First(b => b.Category == BonusCategory.Toughness).Value,
                Is.EqualTo(CommanderBonuses.MaxCombatCompetenceBonus).Within(1e-9));

            var mid = CommanderBonuses.RollCombatCompetence(100);
            Assert.That(mid.First(b => b.Category == BonusCategory.Firepower).Value,
                Is.EqualTo(CommanderBonuses.MaxCombatCompetenceBonus / 2).Within(1e-9), "cap 100 -> half bonus");
            Log("competence -> bonuses scale with ExperienceCap");
        }

        [Test]
        [Description("The generated bonuses fold back through the read helper — the rung-4 loop closes end to end.")]
        public void GeneratedCompetence_FoldsThroughTheReadHelper()
        {
            var bonuses = new BonusesDB();
            foreach (var b in CommanderBonuses.RollCombatCompetence(200))
                bonuses.Bonuses.Add(b);

            Assert.That(CommanderBonuses.CombatMultiplier(bonuses, BonusCategory.Firepower),
                Is.EqualTo(1.0 + CommanderBonuses.MaxCombatCompetenceBonus).Within(1e-9),
                "generate -> read round-trips: a top graduate's firepower multiplier is 1 + the max bonus");
            Log("academy competence -> BonusesDB -> multiplier round-trips");
        }
    }
}
