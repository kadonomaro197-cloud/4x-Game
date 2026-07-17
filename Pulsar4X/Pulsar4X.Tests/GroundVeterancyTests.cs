using NUnit.Framework;
using Pulsar4X.Factions;
using Pulsar4X.GroundCombat;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// VETERANCY / TRAINING (the litmus follow-up "elite units are better — and a training dial reflects in game").
    /// Slice A: <see cref="GroundUnitDesign.TrainingMultiplier"/> is BAKED into a raised unit's Attack + toughness at
    /// <see cref="GroundForces.RaiseUnit"/> — the ground echo of a ship's <c>UnitCaliberAtb</c> Firepower×Toughness stamp.
    /// Gauges: a trained design fields a harder-hitting, tougher unit; an untrained design (default 1.0) is BYTE-IDENTICAL;
    /// the multiplier is applied ONCE (baked), read back as a readout, never re-applied. Drives <c>RaiseUnit</c> directly
    /// with a hand-built design (deterministic) — the same pattern the resolver/upkeep gauges use.
    /// </summary>
    [TestFixture]
    public class GroundVeterancyTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[veterancy] " + m);

        private static GroundUnitDesign Design(string id, double training) =>
            new GroundUnitDesign { UniqueID = id, Name = id, UnitType = GroundUnitType.Infantry, Attack = 100, Defense = 10, HitPoints = 500, TrainingMultiplier = training };

        [Test]
        [Description("A trained design multiplies Attack + toughness at raise; the multiplier is read back as a readout, applied once.")]
        public void Training_MultipliesAttackAndToughness_AtRaise()
        {
            var s = TestScenario.CreateWithColony();
            var body = s.StartingBody;
            int factionId = s.Faction.Id;

            var green = GroundForces.RaiseUnit(body, Design("green", 1.0), factionId, 0);
            var veteran = GroundForces.RaiseUnit(body, Design("veteran", 1.5), factionId, 0);

            // Green (1.0) — byte-identical to the raw design stats.
            Assert.That(green.Attack, Is.EqualTo(100).Within(1e-9), "green Attack unchanged (×1.0)");
            Assert.That(green.MaxHealth, Is.EqualTo(500).Within(1e-9), "green toughness unchanged (×1.0)");
            Assert.That(green.TrainingMultiplier, Is.EqualTo(1.0).Within(1e-9));

            // Veteran (1.5) — Attack + toughness scaled; Defense (armour, equipment not training) unchanged.
            Assert.That(veteran.Attack, Is.EqualTo(150).Within(1e-9), "veteran hits harder (100 × 1.5)");
            Assert.That(veteran.MaxHealth, Is.EqualTo(750).Within(1e-9), "veteran is tougher (500 × 1.5)");
            Assert.That(veteran.Health, Is.EqualTo(750).Within(1e-9), "musters full at the boosted toughness");
            Assert.That(veteran.Defense, Is.EqualTo(10).Within(1e-9), "Defense (armour) is NOT scaled by training");
            Assert.That(veteran.TrainingMultiplier, Is.EqualTo(1.5).Within(1e-9), "readout records the stamp");
            Log($"green {green.Attack:0}/{green.MaxHealth:0}  veteran {veteran.Attack:0}/{veteran.MaxHealth:0} (×{veteran.TrainingMultiplier})");
        }

        [Test]
        [Description("A design that never sets TrainingMultiplier defaults to 1.0 — every existing design is byte-identical.")]
        public void UnsetTraining_DefaultsToOne_ByteIdentical()
        {
            var s = TestScenario.CreateWithColony();
            var body = s.StartingBody;

            // No TrainingMultiplier set — the property initializer (= 1.0) must hold.
            var design = new GroundUnitDesign { UniqueID = "plain", Name = "plain", UnitType = GroundUnitType.Armor, Attack = 140, Defense = 15, HitPoints = 700 };
            Assert.That(design.TrainingMultiplier, Is.EqualTo(1.0).Within(1e-9), "default training multiplier is 1.0");

            var unit = GroundForces.RaiseUnit(body, design, s.Faction.Id, 0);
            Assert.That(unit.Attack, Is.EqualTo(140).Within(1e-9), "unset training → Attack unchanged");
            Assert.That(unit.MaxHealth, Is.EqualTo(700).Within(1e-9), "unset training → toughness unchanged");
        }
    }
}
