using System;
using NUnit.Framework;
using Pulsar4X.Factions;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// M2-0a gauge (docs/AI-BRAIN-BUILD-TRACKER.md, Movement II): the personality data model. Proves an unset trait
    /// reads Neutral (0.5 — the byte-identical value), Set clamps to 0..1, there are exactly 12 traits, and the blob
    /// clones deeply (save/load, entity transfer).
    /// </summary>
    [TestFixture]
    public class PersonalityDBTests
    {
        [Test]
        [Description("Unset = Neutral; Set clamps to [0,1]; 12 traits; deep Clone.")]
        public void Traits_Default_Set_Clamp_Clone()
        {
            var p = new PersonalityDB();

            Assert.That(p.TraitOf(PersonalityTrait.Zealotry), Is.EqualTo(PersonalityDB.Neutral).Within(1e-9),
                "an unset trait reads the neutral 0.5 — the byte-identical value");

            p.SetTrait(PersonalityTrait.Zealotry, 0.9);
            Assert.That(p.TraitOf(PersonalityTrait.Zealotry), Is.EqualTo(0.9).Within(1e-9));

            p.SetTrait(PersonalityTrait.Xenophobia, 1.5); // clamps high
            Assert.That(p.TraitOf(PersonalityTrait.Xenophobia), Is.EqualTo(1.0).Within(1e-9));
            p.SetTrait(PersonalityTrait.Honor, -0.5);      // clamps low
            Assert.That(p.TraitOf(PersonalityTrait.Honor), Is.EqualTo(0.0).Within(1e-9));

            Assert.That(Enum.GetValues(typeof(PersonalityTrait)).Length, Is.EqualTo(12), "the locked 12-trait model");

            var clone = (PersonalityDB)p.Clone();
            Assert.That(clone.TraitOf(PersonalityTrait.Zealotry), Is.EqualTo(0.9).Within(1e-9), "the clone carries the traits");
            clone.SetTrait(PersonalityTrait.Zealotry, 0.1);
            Assert.That(p.TraitOf(PersonalityTrait.Zealotry), Is.EqualTo(0.9).Within(1e-9), "the clone shares no state with the original");
        }
    }
}
