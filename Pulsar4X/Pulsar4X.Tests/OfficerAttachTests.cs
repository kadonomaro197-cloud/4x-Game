using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using NUnit.Framework;
using Pulsar4X.Combat;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Fleets;
using Pulsar4X.People;
using Pulsar4X.Ships;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Phase-2.7-attach gauge: an officer now carries their OWN character on <see cref="CommanderDB"/>, and one live
    /// decision — the fleet's break-off nerve (<see cref="CombatEngagement.ShouldRetreat"/> via
    /// <see cref="CombatEngagement.BlendedRetreatCollectivism"/>) — reads the flagship officer's Collectivism BLENDED
    /// toward the faction's doctrine by the officer's tenure (<see cref="OfficerCharacter.Blend"/> over
    /// <see cref="OfficerCharacter.TenureWeight"/>). Proves: (a) a fresh commander is all-neutral and survives a
    /// save/load round-trip; (b) a GREEN officer (0 tenure) yields the FACTION's trait via the wired read; (c) a
    /// VETERAN officer with a divergent trait shifts the blended value toward their own; (d) a neutral officer is
    /// BYTE-IDENTICAL to the faction-only value the decision used before this wire.
    /// </summary>
    [TestFixture]
    public class OfficerAttachTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[officer-attach] " + m);

        // Matches the game's save/load serialization (TypeNameHandling.Objects + PreserveReferencesHandling.Objects +
        // the non-public-setter resolver) so this round-trip is the real thing, not a looser stand-in.
        private static readonly JsonSerializerSettings SaveSettings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Objects,
            PreserveReferencesHandling = PreserveReferencesHandling.Objects,
            ContractResolver = new NonPublicResolver()
        };

        /// <summary>Build a fleet whose flagship is crewed by an officer with the given tenure + Collectivism.</summary>
        private static Entity FleetWithFlagshipOfficer(TestScenario s, int experience, int experienceCap, double? officerCollectivism)
        {
            var fleet = FleetFactory.Create(s.StartingSystem, s.Faction.Id, "Task Force");
            var design = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns.Values.First();
            var flagship = ShipFactory.CreateShip(design, s.Faction, s.StartingBody, "Flagship");
            s.Game.OrderHandler.HandleOrder(FleetOrder.AssignShip(s.Faction.Id, fleet, flagship));
            fleet.GetDataBlob<FleetDB>().FlagShipID = flagship.Id;

            var commanderDB = new CommanderDB("Adm. Test", 1, CommanderTypes.Navy);
            commanderDB.Experience = experience;
            commanderDB.ExperienceCap = experienceCap;
            if (officerCollectivism.HasValue)
                commanderDB.Personality.SetTrait(PersonalityTrait.Collectivism, officerCollectivism.Value);
            var commander = CommanderFactory.Create(s.StartingSystem, s.Faction.Id, commanderDB);
            flagship.GetDataBlob<ShipInfoDB>().CommanderID = commander.Id;
            return fleet;
        }

        private static PersonalityDB Faction(double collectivism)
        {
            var p = new PersonalityDB();
            p.SetTrait(PersonalityTrait.Collectivism, collectivism);
            return p;
        }

        [Test]
        [Description("(a) A fresh commander is all-neutral (0.5) and its authored personality survives a save/load round-trip.")]
        public void FreshCommander_IsNeutral_AndRoundTrips()
        {
            var fresh = new CommanderDB("New Officer", 1, CommanderTypes.Navy);
            Assert.That(fresh.Personality, Is.Not.Null, "a fresh commander always has a personality object");
            Assert.That(fresh.Personality.TraitOf(PersonalityTrait.Collectivism),
                Is.EqualTo(PersonalityDB.Neutral).Within(1e-9), "an unauthored officer reads neutral 0.5");

            // Author a divergent trait, round-trip through the real save/load serialization, assert it survives.
            var authored = new CommanderDB("Bold Officer", 1, CommanderTypes.Navy);
            authored.Experience = 80;
            authored.ExperienceCap = 100;
            authored.Personality.SetTrait(PersonalityTrait.Collectivism, 0.9);

            string json = JsonConvert.SerializeObject(authored, SaveSettings);
            var reloaded = JsonConvert.DeserializeObject<CommanderDB>(json, SaveSettings);

            Assert.That(reloaded, Is.Not.Null);
            Assert.That(reloaded.Experience, Is.EqualTo(80));
            Assert.That(reloaded.ExperienceCap, Is.EqualTo(100));
            Assert.That(reloaded.Personality.TraitOf(PersonalityTrait.Collectivism),
                Is.EqualTo(0.9).Within(1e-9), "the authored officer trait survives save/load");
            Log($"round-trip: fresh=0.5, authored=0.9 → reloaded={reloaded.Personality.TraitOf(PersonalityTrait.Collectivism)}");
        }

        [Test]
        [Description("(b)+(d) A GREEN officer (0 tenure) yields the faction's own trait — byte-identical to the faction-only value.")]
        public void GreenOfficer_YieldsFactionTrait_ByteIdentical()
        {
            var s = TestScenario.CreateWithColony();
            var faction = Faction(0.8);
            // Green = full experience cap 0 (or experience 0) → tenure weight 0, even with a DIVERGENT officer trait.
            var fleet = FleetWithFlagshipOfficer(s, experience: 0, experienceCap: 100, officerCollectivism: 0.1);

            double blended = CombatEngagement.BlendedRetreatCollectivism(fleet, faction);
            Assert.That(blended, Is.EqualTo(0.8).Within(1e-9),
                "a green officer follows doctrine → the faction's Collectivism, not their own 0.1");

            // And the whole threshold read is byte-identical to the pre-wire faction-only path.
            double wired = CombatEngagement.RetreatThresholdForCollectivism(blended);
            double factionOnly = CombatEngagement.RetreatThresholdFor(faction);
            Assert.That(wired, Is.EqualTo(factionOnly).Within(1e-9), "green officer → identical retreat threshold");
            Log($"green: blended={blended} (faction 0.8, officer 0.1), threshold {wired} == faction-only {factionOnly}");
        }

        [Test]
        [Description("(c) A VETERAN officer with a divergent trait shifts the blended value toward their own character.")]
        public void VeteranOfficer_ShiftsBlendedTrait()
        {
            var s = TestScenario.CreateWithColony();
            var faction = Faction(0.8);
            // Veteran = experience at the cap → tenure weight 1 → the officer's own trait dominates.
            var veteranFleet = FleetWithFlagshipOfficer(s, experience: 100, experienceCap: 100, officerCollectivism: 0.1);
            double veteran = CombatEngagement.BlendedRetreatCollectivism(veteranFleet, faction);
            Assert.That(veteran, Is.EqualTo(0.1).Within(1e-9),
                "a maxed-tenure officer runs on their own Collectivism (0.1), overriding the faction's 0.8");

            // A half-tenure officer lands between the faction and their own value.
            var midFleet = FleetWithFlagshipOfficer(s, experience: 50, experienceCap: 100, officerCollectivism: 0.1);
            double mid = CombatEngagement.BlendedRetreatCollectivism(midFleet, faction);
            Assert.That(mid, Is.EqualTo(0.8 + (0.1 - 0.8) * 0.5).Within(1e-9), "half tenure = a linear mix (0.45)");
            Assert.That(mid, Is.LessThan(0.8).And.GreaterThan(0.1), "the blend sits between doctrine and the officer");
            Log($"veteran={veteran} (own 0.1 wins), half-tenure={mid} (between 0.8 and 0.1)");
        }

        [Test]
        [Description("(d) A DEFAULT officer (0 experience / 0 cap) leaves the decision at the faction value — the byte-identical state of every existing fixture.")]
        public void NeutralOfficer_IsByteIdentical()
        {
            var s = TestScenario.CreateWithColony();
            var faction = Faction(0.8);

            // THE load-bearing byte-identity: a DEFAULT commander (Experience 0, Cap 0 — what a fresh academy
            // graduate has, and what every existing combat fixture that seats a commander has) → tenure weight 0 →
            // the faction's own value EXACTLY, regardless of any officer character.
            var defaultOfficerFleet = FleetWithFlagshipOfficer(s, experience: 0, experienceCap: 0, officerCollectivism: null);
            double defaultBlended = CombatEngagement.BlendedRetreatCollectivism(defaultOfficerFleet, faction);
            Assert.That(defaultBlended, Is.EqualTo(0.8).Within(1e-9),
                "a default (0-experience) officer defers entirely to doctrine → the faction's Collectivism unchanged");

            // A fleet with NO flagship officer at all is likewise exactly the faction value — the other existing state.
            var bareFleet = FleetFactory.Create(s.StartingSystem, s.Faction.Id, "Bare Fleet");
            double bare = CombatEngagement.BlendedRetreatCollectivism(bareFleet, faction);
            Assert.That(bare, Is.EqualTo(0.8).Within(1e-9), "no flagship officer → the faction's own value, unchanged");
            Assert.That(CombatEngagement.BlendedRetreatCollectivism(bareFleet, null),
                Is.EqualTo(PersonalityDB.Neutral).Within(1e-9), "no officer + no faction personality → neutral (the old default)");

            // A seasoned officer whose OWN character is neutral (0.5) faithfully runs on 0.5 — this is the blend
            // working as designed (a veteran asserts their character), NOT a byte-identity case; only DEFAULT-tenure
            // or bare fleets are byte-identical to the pre-wire decision.
            var neutralVet = FleetWithFlagshipOfficer(s, experience: 100, experienceCap: 100, officerCollectivism: null);
            double vet = CombatEngagement.BlendedRetreatCollectivism(neutralVet, faction);
            Assert.That(vet, Is.EqualTo(PersonalityDB.Neutral).Within(1e-9),
                "a maxed-tenure officer with an unauthored (neutral) character runs on their own 0.5");
            Log($"default officer={defaultBlended} (== faction 0.8); bare={bare}; neutral-veteran={vet}");
        }
    }
}
