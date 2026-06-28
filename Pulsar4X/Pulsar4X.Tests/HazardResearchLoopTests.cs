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
    }
}
