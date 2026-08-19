using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pulsar4X.Combat;       // AuraAtb, AuraEffect, AuraTarget
using Pulsar4X.Components;   // ComponentDesign, ComponentInstance
using Pulsar4X.Datablobs;    // ComponentInstancesDB
using Pulsar4X.Engine;
using Pulsar4X.Galaxy;       // PlanetRegionsFactory
using Pulsar4X.GroundCombat;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// E14 — AURAS, slice 3b (Fork B, the GROUND half — "also applies to planetary combat"). Mirrors the space
    /// fleet-wide fold: a faction's aura BUILDING on the body buffs ALL its battalions there (Command→firepower,
    /// Ward→toughness, strongest wins). The building is scanned off the colony's <c>ComponentInstancesDB</c> exactly
    /// as <c>GroundFortification</c> reads a <c>GroundDefenseAtb</c> bunker. Flag-gated
    /// (<see cref="GroundCommandAura.EnableGroundCommandAura"/>) default OFF → byte-identical. Engine-only → CI.
    /// </summary>
    [TestFixture]
    public class GroundCommandAuraTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[ground-aura] " + m);

        private const int InvaderFaction = 900001;

        [TearDown]
        public void ResetFlag() => GroundCommandAura.EnableGroundCommandAura = false;

        private static GroundUnitDesign Tank() => new GroundUnitDesign
        {
            UniqueID = "test-aura-tank",
            Name = "Test Tank",
            UnitType = GroundUnitType.Infantry,
            Attack = 100,
            Defense = 10,
            HitPoints = 100000,   // fat HP so a few salvos never wipe a side — the buff shows as a health delta
            IndustryPointCosts = 100,
            IndustryTypeID = "installation",
            ResourceCosts = new Dictionary<string, long>(),
        };

        /// <summary>Install a friendly aura command BUILDING (a component carrying an <see cref="AuraAtb"/>) onto the
        /// scenario's colony — the ground aura source, found off the body's component stores.</summary>
        private static void InstallAura(TestScenario s, AuraEffect effect, double magnitude, AuraTarget target = AuraTarget.Friends)
        {
            var comps = s.Colony.GetDataBlob<ComponentInstancesDB>();
            var design = new ComponentDesign { UniqueID = "test-aura-hq-" + effect + "-" + target, Name = "Command Beacon" };
            design.AttributesByType[typeof(AuraAtb)] = new AuraAtb(0, magnitude, (double)(int)effect, (double)(int)target);
            comps.AddComponentInstance(new ComponentInstance(design));
        }

        /// <summary>Run a 1-defender-vs-1-invader fight in region 0 and return each side's surviving total health.
        /// The defender's faction optionally has an aura building.</summary>
        private static (double defender, double invader) Fight(bool withBuilding, AuraEffect effect, double magnitude, int salvos)
        {
            var s = TestScenario.CreateWithColony();
            PlanetRegionsFactory.GenerateForSystem(s.StartingSystem, surveyed: true);
            var body = s.StartingBody;
            var design = Tank();

            GroundForces.RaiseUnit(body, design, s.Faction.Id, 0);     // the defender
            GroundForces.RaiseUnit(body, design, InvaderFaction, 0);   // the invader

            if (withBuilding)
                InstallAura(s, effect, magnitude);

            var proc = new GroundForcesProcessor();
            for (int i = 0; i < salvos; i++) proc.ProcessEntity(body, 3600);

            var forces = body.GetDataBlob<GroundForcesDB>();
            double def = forces.Units.Where(u => u.FactionOwnerID == s.Faction.Id).Sum(u => u.Health);
            double inv = forces.Units.Where(u => u.FactionOwnerID == InvaderFaction).Sum(u => u.Health);
            return (def, inv);
        }

        [Test]
        [Description("MultFor reads a friendly aura building off the body's stores — the right faction + effect only, "
                     + "and flag-gated.")]
        public void MultFor_ReadsBuilding_RightFactionAndEffect_FlagGated()
        {
            var s = TestScenario.CreateWithColony();
            var body = s.StartingBody;
            InstallAura(s, AuraEffect.Command, 0.5);   // a Command 0.5 building for the colony's faction

            GroundCommandAura.EnableGroundCommandAura = true;
            Assert.That(GroundCommandAura.MultFor(body, s.Faction.Id, AuraEffect.Command), Is.EqualTo(1.5).Within(1e-9), "a Command building → ×1.5 firepower");
            Assert.That(GroundCommandAura.MultFor(body, s.Faction.Id, AuraEffect.Ward), Is.EqualTo(1.0).Within(1e-9), "a Command building doesn't answer for Ward");
            Assert.That(GroundCommandAura.MultFor(body, InvaderFaction, AuraEffect.Command), Is.EqualTo(1.0).Within(1e-9), "another faction doesn't get our building");

            GroundCommandAura.EnableGroundCommandAura = false;
            Assert.That(GroundCommandAura.MultFor(body, s.Faction.Id, AuraEffect.Command), Is.EqualTo(1.0).Within(1e-9), "flag off → no buff (byte-identical)");
        }

        [Test]
        [Description("A Foes-targeted building does not buff its own side.")]
        public void FoesBuilding_DoesNotBuffOwnSide()
        {
            var s = TestScenario.CreateWithColony();
            var body = s.StartingBody;
            InstallAura(s, AuraEffect.Command, 0.5, AuraTarget.Foes);

            GroundCommandAura.EnableGroundCommandAura = true;
            Assert.That(GroundCommandAura.MultFor(body, s.Faction.Id, AuraEffect.Command), Is.EqualTo(1.0).Within(1e-9), "a Foes field doesn't buff its own side");
        }

        [Test]
        [Description("A friendly COMMAND aura building grinds the enemy down harder — the defender's battalion hits "
                     + "for more, so the invader ends with less health than in the same fight with no aura.")]
        public void CommandAura_RaisesBattalionFirepower()
        {
            GroundCommandAura.EnableGroundCommandAura = true;
            var withAura = Fight(withBuilding: true, AuraEffect.Command, magnitude: 1.0, salvos: 3);
            var noAura = Fight(withBuilding: false, AuraEffect.Command, magnitude: 1.0, salvos: 3);

            Assert.That(withAura.invader, Is.LessThan(noAura.invader),
                "the defender's command aura ground the invader down harder (more firepower)");
            Log($"invader health: {noAura.invader:0} (no aura) → {withAura.invader:0} (command aura)");
        }

        [Test]
        [Description("A friendly WARD aura building toughens the battalion — the defender takes less damage, so it "
                     + "ends the same fight with more health than with no aura. Flag OFF → byte-identical.")]
        public void WardAura_ToughensBattalion_FlagOffByteIdentical()
        {
            GroundCommandAura.EnableGroundCommandAura = true;
            var withWard = Fight(withBuilding: true, AuraEffect.Ward, magnitude: 1.0, salvos: 3);
            var noWard = Fight(withBuilding: false, AuraEffect.Ward, magnitude: 1.0, salvos: 3);
            Assert.That(withWard.defender, Is.GreaterThan(noWard.defender),
                "the defender's ward aura let it keep more health (less damage taken)");

            // Flag OFF: the SAME building present, but the fold is a no-op → identical to no building.
            GroundCommandAura.EnableGroundCommandAura = false;
            var wardOff = Fight(withBuilding: true, AuraEffect.Ward, magnitude: 1.0, salvos: 3);
            Assert.That(wardOff.defender, Is.EqualTo(noWard.defender).Within(1e-6),
                "flag off with the building present → byte-identical to no building");
            Log($"defender health: {noWard.defender:0} (no aura) → {withWard.defender:0} (ward aura); flag-off {wardOff.defender:0}");
        }
    }
}
