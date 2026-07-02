using System.Linq;
using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Ships;
using Pulsar4X.Hazards;
using Pulsar4X.Damage;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// The WHOLE survey→discover→research→armour loop, end to end on a real faction — the cradle-to-grave proof:
    /// <list type="number">
    /// <item>the counter-tech starts LOCKED and the rated armour is unavailable;</item>
    /// <item>a ship discovers a thermal hazard → the counter-tech OPENS (becomes researchable);</item>
    /// <item>completing the tech UNLOCKS the rated armour (nickel-steel) + its build material;</item>
    /// <item>that armour's material carries real thermal <see cref="DamageSignature"/> resistance — so a ship clad
    /// in it actually shrugs off the corona (the wiring proven in ArmorMaterialWiringTests).</item>
    /// </list>
    /// Research it → build it → it resists. All the data (Stellar Science category, tech-thermal-shielding, the
    /// nickel-steel resistance blueprint) loads from the base mod through the real New-Game path.
    /// </summary>
    [TestFixture]
    public class HazardResearchLoopTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[research-loop] " + m);

        private static SpaceHazardDB ThermalHazard() => new SpaceHazardDB
        {
            HazardType = SpaceHazardType.StarCorona,
            Radius_m = 1e10,
            Effects = { new HazardEffect(HazardEffectType.HeatDamage, 100, 10000, scalesWithProximity: true) },
        };

        private static SpaceHazardDB RadiationHazard() => new SpaceHazardDB
        {
            HazardType = SpaceHazardType.SolarFlare,
            Radius_m = 1e10,
            Effects = { new HazardEffect(HazardEffectType.RadiationDamage, 500, 150) }, // UV/ionising, like the real flare
        };

        private static SpaceHazardDB KineticHazard() => new SpaceHazardDB
        {
            HazardType = SpaceHazardType.Generic,
            Radius_m = 1e10,
            Effects = { new HazardEffect(HazardEffectType.KineticDamage, 60, 0) }, // micrometeoroids, like the real debris field
        };

        [Test]
        [Description("Discover a thermal hazard → its counter-research opens → completing it unlocks heat-resistant " +
                     "armour whose material actually resists thermal damage. The full cradle-to-grave loop.")]
        public void DiscoverThermalHazard_ResearchUnlocksThermalRatedArmour()
        {
            var s = TestScenario.CreateWithColony();
            var factionInfo = s.Faction.GetDataBlob<FactionInfoDB>();
            var data = factionInfo.Data;
            var design = factionInfo.ShipDesigns.Values.First();

            // 1. Before discovery: the counter-tech is LOCKED (not researchable), the rated armour unavailable.
            Assert.That(data.LockedTechs.ContainsKey("tech-thermal-shielding"), Is.True, "the thermal counter-tech starts LOCKED");
            Assert.That(data.Techs.ContainsKey("tech-thermal-shielding"), Is.False, "...so it isn't researchable yet");
            Assert.That(data.Armor.ContainsKey("nickel-steel-armor"), Is.False, "the rated armour is unavailable before research");

            // 2. A ship discovers a thermal hazard → the counter-research opens.
            var ship = ShipFactory.CreateShip(design, s.Faction, s.StartingBody, "Surveyor");
            HazardDiscovery.RecordAndAnnounce(ship, ThermalHazard(), s.Game.TimePulse.GameGlobalDateTime);
            Assert.That(data.Techs.ContainsKey("tech-thermal-shielding"), Is.True,
                "discovering a thermal hazard opens its counter-research");

            // 3. Complete the research → unlocks the rated armour (and its build material).
            var tech = data.Techs["tech-thermal-shielding"];
            data.IncrementTechLevel(tech);
            Assert.That(data.Armor.ContainsKey("nickel-steel-armor"), Is.True,
                "completing the counter-tech unlocks the heat-resistant armour");

            // 4. The unlocked armour's material carries real thermal resistance (the payoff — clad a ship, survive the corona).
            byte id = DamageTools.IDCodeForMaterial("nickel-steel");
            var resist = DamageTools.DamageResistsLookupTable[id];
            float thermalResist = resist.SignatureResistance[(int)DamageSignature.Thermal];
            Log($"nickel-steel IDCode={id}, thermal resistance={thermalResist}");
            Assert.That(thermalResist, Is.GreaterThan(0f),
                "the unlocked armour's material must actually resist thermal damage — the loop pays off");
        }

        [Test]
        [Description("The SECOND fully-wired flavour: discover a RADIATION hazard (a solar flare) → its counter-research " +
                     "opens → completing it unlocks tungsten radiation plating whose material actually resists hard " +
                     "radiation. Proves the keystone pattern repeats — a real second worked example, end to end.")]
        public void DiscoverRadiationHazard_ResearchUnlocksRadiationRatedArmour()
        {
            var s = TestScenario.CreateWithColony();
            var factionInfo = s.Faction.GetDataBlob<FactionInfoDB>();
            var data = factionInfo.Data;
            var design = factionInfo.ShipDesigns.Values.First();

            // 1. Before discovery: the radiation counter-tech is LOCKED, the rated armour unavailable.
            Assert.That(data.LockedTechs.ContainsKey("tech-radiation-shielding"), Is.True, "the radiation counter-tech starts LOCKED");
            Assert.That(data.Techs.ContainsKey("tech-radiation-shielding"), Is.False, "...so it isn't researchable yet");
            Assert.That(data.Armor.ContainsKey("tungsten-plating-armor"), Is.False, "the rated armour is unavailable before research");

            // 2. A ship caught in a solar flare discovers hard radiation → the counter-research opens.
            var ship = ShipFactory.CreateShip(design, s.Faction, s.StartingBody, "Surveyor");
            HazardDiscovery.RecordAndAnnounce(ship, RadiationHazard(), s.Game.TimePulse.GameGlobalDateTime);
            Assert.That(data.Techs.ContainsKey("tech-radiation-shielding"), Is.True,
                "discovering a radiation hazard opens its counter-research");

            // 3. Complete the research → unlocks the rated armour (and its tungsten build material).
            var tech = data.Techs["tech-radiation-shielding"];
            data.IncrementTechLevel(tech);
            Assert.That(data.Armor.ContainsKey("tungsten-plating-armor"), Is.True,
                "completing the counter-tech unlocks the radiation-resistant armour");

            // 4. The unlocked armour's material carries real hard-radiation resistance (clad a ship, survive the flare).
            byte id = DamageTools.IDCodeForMaterial("tungsten-plating");
            var resist = DamageTools.DamageResistsLookupTable[id];
            float radResist = resist.SignatureResistance[(int)DamageSignature.HardRadiation];
            Log($"tungsten-plating IDCode={id}, hard-radiation resistance={radResist}");
            Assert.That(radResist, Is.GreaterThan(0f),
                "the unlocked armour's material must actually resist hard radiation — the loop pays off");
        }

        [Test]
        [Description("The THIRD fully-wired flavour: discover a KINETIC hazard (a debris field) → its counter-research " +
                     "opens → completing it unlocks ablative composite armour whose material actually resists kinetic " +
                     "damage. Kinetic rides the wavelength-0 armour path, same as a railgun slug.")]
        public void DiscoverKineticHazard_ResearchUnlocksKineticRatedArmour()
        {
            var s = TestScenario.CreateWithColony();
            var factionInfo = s.Faction.GetDataBlob<FactionInfoDB>();
            var data = factionInfo.Data;
            var design = factionInfo.ShipDesigns.Values.First();

            // 1. Before discovery: the kinetic counter-tech is LOCKED, the rated armour unavailable.
            Assert.That(data.LockedTechs.ContainsKey("tech-ablative-plating"), Is.True, "the kinetic counter-tech starts LOCKED");
            Assert.That(data.Techs.ContainsKey("tech-ablative-plating"), Is.False, "...so it isn't researchable yet");
            Assert.That(data.Armor.ContainsKey("ablative-composite-armor"), Is.False, "the rated armour is unavailable before research");

            // 2. A ship in a debris field discovers kinetic impacts → the counter-research opens.
            var ship = ShipFactory.CreateShip(design, s.Faction, s.StartingBody, "Surveyor");
            HazardDiscovery.RecordAndAnnounce(ship, KineticHazard(), s.Game.TimePulse.GameGlobalDateTime);
            Assert.That(data.Techs.ContainsKey("tech-ablative-plating"), Is.True,
                "discovering a kinetic hazard opens its counter-research");

            // 3. Complete the research → unlocks the rated armour (and its composite build material).
            var tech = data.Techs["tech-ablative-plating"];
            data.IncrementTechLevel(tech);
            Assert.That(data.Armor.ContainsKey("ablative-composite-armor"), Is.True,
                "completing the counter-tech unlocks the kinetic-resistant armour");

            // 4. The unlocked armour's material carries real kinetic resistance (clad a ship, cross the debris field).
            byte id = DamageTools.IDCodeForMaterial("ablative-composite");
            var resist = DamageTools.DamageResistsLookupTable[id];
            float kineticResist = resist.SignatureResistance[(int)DamageSignature.Kinetic];
            Log($"ablative-composite IDCode={id}, kinetic resistance={kineticResist}");
            Assert.That(kineticResist, Is.GreaterThan(0f),
                "the unlocked armour's material must actually resist kinetic damage — the loop pays off");
        }

        // The three NON-WAVELENGTH flavours share one loop shape — discover the hazard, the counter-tech opens,
        // research unlocks the rated armour, the material resists the flavour. DRY helper drives all three.
        private static void AssertHazardLoop(HazardEffectType effect, string techId, string armorId,
                                             string materialId, DamageSignature sig)
        {
            var s = TestScenario.CreateWithColony();
            var data = s.Faction.GetDataBlob<FactionInfoDB>().Data;
            var design = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns.Values.First();

            Assert.That(data.LockedTechs.ContainsKey(techId), Is.True, $"{techId} starts LOCKED");
            Assert.That(data.Techs.ContainsKey(techId), Is.False, $"{techId} not researchable before discovery");
            Assert.That(data.Armor.ContainsKey(armorId), Is.False, $"{armorId} unavailable before research");

            var haz = new SpaceHazardDB { HazardType = SpaceHazardType.Generic, Radius_m = 1e10,
                Effects = { new HazardEffect(effect, 100, 0) } };
            var ship = ShipFactory.CreateShip(design, s.Faction, s.StartingBody, "Surveyor");
            HazardDiscovery.RecordAndAnnounce(ship, haz, s.Game.TimePulse.GameGlobalDateTime);
            Assert.That(data.Techs.ContainsKey(techId), Is.True, $"discovering the hazard opens {techId}");

            data.IncrementTechLevel(data.Techs[techId]);
            Assert.That(data.Armor.ContainsKey(armorId), Is.True, $"completing {techId} unlocks {armorId}");

            byte id = DamageTools.IDCodeForMaterial(materialId);
            float resist = DamageTools.DamageResistsLookupTable[id].SignatureResistance[(int)sig];
            Log($"{materialId} IDCode={id}, {sig} resistance={resist}");
            Assert.That(resist, Is.GreaterThan(0f), $"{materialId} must actually resist {sig} — the loop pays off");
        }

        [Test]
        [Description("4th flavour — Corrosive: a corrosive nebula (gas cloud) → tech-corrosion-plating → corrosion-resistant alloy.")]
        public void DiscoverCorrosiveHazard_ResearchUnlocksCorrosionRatedArmour() =>
            AssertHazardLoop(HazardEffectType.CorrosiveDamage, "tech-corrosion-plating",
                "corrosion-resistant-alloy-armor", "corrosion-resistant-alloy", DamageSignature.Corrosive);

        [Test]
        [Description("5th flavour — EMStorm: an ion storm → tech-em-hardening → EM shielding mesh.")]
        public void DiscoverEMHazard_ResearchUnlocksEMRatedArmour() =>
            AssertHazardLoop(HazardEffectType.EMDamage, "tech-em-hardening",
                "em-shielding-mesh-armor", "em-shielding-mesh", DamageSignature.EMStorm);

        [Test]
        [Description("6th flavour — Gravimetric: a gravimetric anomaly → tech-structural-reinforcement → reinforced trusswork.")]
        public void DiscoverGravimetricHazard_ResearchUnlocksStructuralArmour() =>
            AssertHazardLoop(HazardEffectType.GravimetricDamage, "tech-structural-reinforcement",
                "reinforced-trusswork-armor", "reinforced-trusswork", DamageSignature.Gravimetric);
    }
}
