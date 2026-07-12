using NUnit.Framework;
using Newtonsoft.Json.Linq;
using Pulsar4X.Factions;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Phase 5.1a gauge (docs/AI-BRAIN-BUILD-TRACKER.md — 🪐 The Brane, authoring). Proves a scenario can AUTHOR a
    /// faction's personality from data: <see cref="FactionFactory.PersonalityFromJson"/> turns a JSON object of
    /// <c>traitName → 0..1</c> into a <see cref="PersonalityDB"/> the whole brain reads. Trait names are
    /// case-insensitive, unknown names and null values are skipped, and any omitted trait stays Neutral — so a
    /// scenario with NO personality node yields an all-neutral (byte-identical) faction. This is the data half of the
    /// north-star acceptance test: hand a faction a character in JSON, not in C#.
    /// </summary>
    [TestFixture]
    public class PersonalityAuthoringTests
    {
        [Test]
        [Description("A JSON trait block authors the matching PersonalityDB traits; case-insensitive; unknown/null skipped; omitted stays Neutral.")]
        public void PersonalityFromJson_AuthorsTheTraits()
        {
            // A warlike, faithless character — note mixed case ("Aggression"/"honor") and a bogus key that must be ignored.
            var node = JObject.Parse(@"{
                ""Aggression"": 0.85,
                ""honor"": 0.15,
                ""xenophobia"": 0.7,
                ""not_a_real_trait"": 0.99,
                ""curiosity"": null
            }");

            var p = FactionFactory.PersonalityFromJson(node);

            Assert.That(p.TraitOf(PersonalityTrait.Aggression), Is.EqualTo(0.85).Within(1e-9), "authored high aggression");
            Assert.That(p.TraitOf(PersonalityTrait.Honor), Is.EqualTo(0.15).Within(1e-9), "authored low honour (case-insensitive key)");
            Assert.That(p.TraitOf(PersonalityTrait.Xenophobia), Is.EqualTo(0.7).Within(1e-9), "authored high xenophobia");

            // A null value is skipped → stays Neutral (not forced to 0).
            Assert.That(p.TraitOf(PersonalityTrait.Curiosity), Is.EqualTo(PersonalityDB.Neutral).Within(1e-9),
                "a null trait value is skipped — the trait stays Neutral");
            // A trait the JSON never mentioned stays Neutral.
            Assert.That(p.TraitOf(PersonalityTrait.Ambition), Is.EqualTo(PersonalityDB.Neutral).Within(1e-9),
                "an omitted trait stays Neutral");
        }

        [Test]
        [Description("An empty personality block authors an all-neutral faction — the byte-identical case.")]
        public void PersonalityFromJson_Empty_IsAllNeutral()
        {
            var p = FactionFactory.PersonalityFromJson(JObject.Parse("{}"));
            foreach (PersonalityTrait trait in System.Enum.GetValues(typeof(PersonalityTrait)))
                Assert.That(p.TraitOf(trait), Is.EqualTo(PersonalityDB.Neutral).Within(1e-9),
                    $"{trait} stays Neutral with no authored value");
        }
    }
}
