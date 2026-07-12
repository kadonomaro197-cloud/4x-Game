using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pulsar4X.Combat;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Fleets;
using Pulsar4X.Ships;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Weapons pilot W5 (heat → sustained-rate for energy weapons) — SLICE W5a, the byte-identical foundation. The heat
    /// twin of the ammo magazine (W3): the magazine limits how long the KINETIC guns fire, the radiator limits how hard
    /// the ENERGY guns fire SUSTAINED. W5a adds the pieces WITHOUT wiring the throttle: the <see cref="RadiatorAtb"/>
    /// component (the heat sink), <see cref="ShipCombatValueDB.HeatCapacity_kJ"/> (sum of installed radiators), and the
    /// fleet's <see cref="FleetCombatStateDB.HeatPool_kJ"/> (starts cold at 0).
    ///
    /// The invariant this pins: with NO radiator a ship reads 0 heat capacity → the heat step is skipped → combat is
    /// byte-identical (every current ship, until the W5c base-mod radiator). W5b wires the per-salvo heat accumulation +
    /// throttle; W5c adds the buildable base-mod radiator on a beam-heavy ship. Engine-only → runs in CI.
    /// </summary>
    [TestFixture]
    public class ShipHeatTests
    {
        private const string BeamShip = "default-ship-design-test-warship"; // Aegis — 4 lasers, no radiator yet
        private static void Log(string m) => TestContext.Progress.WriteLine("[ship-heat] " + m);

        [Test]
        [Description("RadiatorAtb (the heat sink) holds its kJ capacity and clamps a negative to 0; the fleet heat pool defaults to 0 (cold).")]
        public void RadiatorAtb_AndHeatPool_PinTheFoundation()
        {
            var rad = new RadiatorAtb(4000);
            Assert.That(rad.Capacity_kJ, Is.EqualTo(4000), "the radiator holds its kJ capacity");
            Assert.That(new RadiatorAtb(-10).Capacity_kJ, Is.EqualTo(0), "a negative capacity clamps to 0");
            Assert.That(((RadiatorAtb)rad.Clone()).Capacity_kJ, Is.EqualTo(4000), "clone preserves the capacity");

            Assert.That(new FleetCombatStateDB().HeatPool_kJ, Is.EqualTo(0), "the fleet heat pool starts cold (0)");
        }

        [Test]
        [Description("W5a additive/byte-identical: a real base-mod beam warship with NO radiator reads HeatCapacity_kJ == 0 (so its fleet heat step is skipped and the resolve is untouched), exactly as an unshielded/magazine-less ship reads 0.")]
        public void ARealBeamShip_WithNoRadiator_ReadsZeroHeatCapacity()
        {
            var s = TestScenario.CreateWithColony();
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns;
            Assert.That(designs.ContainsKey(BeamShip), Is.True, "the Aegis beam warship loads onto the faction");

            var ship = ShipFactory.CreateShip(designs[BeamShip], s.Faction, s.StartingBody, "Aegis");
            var cv = ship.GetDataBlob<ShipCombatValueDB>();
            Log($"Aegis (no radiator): firepower={cv.Firepower:0}, heatCapacity={cv.HeatCapacity_kJ:0} kJ");

            Assert.That(cv.Firepower, Is.GreaterThan(0), "the Aegis carries its lasers (energy fire)");
            Assert.That(cv.HeatCapacity_kJ, Is.EqualTo(0),
                "no radiator → 0 heat capacity → the fleet heat step stays disabled → combat byte-identical until the W5c radiator");
        }

        private const string EmberShip = "default-ship-design-test-ember"; // pulse-lasers (hot) + heat-radiators

        [Test]
        [Description("W5c cradle-to-grave: the base-mod Ember Pulse Cruiser builds from JSON — its pulse-lasers project a HOT energy WeaponProfile (HeatPerSecond > 0, via the beam atb's Combat Heat dial → the 8-arg ctor, the exact-arity binder pattern) and its heat-radiators give it a real HeatCapacity_kJ. So a player-built hot-beam ship carries both the heat SOURCE and the SINK the W5b resolver balances — designed → built → installed → must be cooled.")]
        public void TheEmberPulseCruiser_HasHotBeamsAndRadiators_FromJson()
        {
            var s = TestScenario.CreateWithColony();
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns;
            Assert.That(designs.ContainsKey(EmberShip), Is.True,
                "the Ember loads onto the faction — the JSON pulse-laser + heat-radiator templates + designs + earth.json entries wired up (six-point registration)");

            var ship = ShipFactory.CreateShip(designs[EmberShip], s.Faction, s.StartingBody, "Ember");
            var cv = ship.GetDataBlob<ShipCombatValueDB>();
            double weaponHeat = cv.Weapons.Sum(w => w.HeatPerSecond);
            Log($"Ember: firepower={cv.Firepower:0}, weapon heat={weaponHeat:0} kJ/s, radiator capacity={cv.HeatCapacity_kJ:0} kJ");

            Assert.That(cv.Firepower, Is.GreaterThan(0), "the Ember carries its pulse-lasers (energy fire)");
            Assert.That(weaponHeat, Is.GreaterThan(0),
                "the pulse-lasers project a HOT WeaponProfile — the beam atb's Combat Heat dial (8-arg ctor) flows into HeatPerSecond");
            Assert.That(cv.HeatCapacity_kJ, Is.GreaterThan(0),
                "its heat-radiators give it a real heat ceiling — JSON heat-radiator template → RadiatorAtb → ShipCombatValueDB is wired");
        }

        [Test]
        [Description("W5b pure helper: EnergyHeatGen sums each weapon's HeatPerSecond — a cool weapon (HeatPerSecond 0) contributes no heat, a hot one does.")]
        public void EnergyHeatGen_SumsWeaponHeat()
        {
            var mix = new List<WeaponProfile>
            {
                new WeaponProfile(1000, 3e8, 0.95, 0.5, 0, WeaponNature.Energy, WeaponDelivery.Beam, 0, 0, heatPerSecond: 5000),  // hot
                new WeaponProfile(600,  3e8, 0.95, 0.5, 0, WeaponNature.Energy, WeaponDelivery.Beam),                              // cool (0)
            };
            Assert.That(CombatEngagement.EnergyHeatGen(mix), Is.EqualTo(5000), "only the hot beam contributes heat");
        }

        // A controlled heat battle: an attacker firing ONLY a HOT energy weapon at a passive defender. The attacker's
        // radiator capacity is the only variable — enough radiators sustain the fire, too few overheat and throttle it.
        private static int RunHeatBattle(TestScenario s, Entity red, double attackerRadiatorCapacity)
        {
            var design = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns.Values.First();

            var atkFleet = FleetFactory.Create(s.StartingSystem, red.Id, "Hot Beamer");
            var gun = ShipFactory.CreateShip(design, s.Faction, s.StartingBody, "Beamer");
            gun.FactionOwnerID = red.Id;
            var hotBeam = new WeaponProfile(40_000, 3e8, 0.95, 0.5, 0, WeaponNature.Energy, WeaponDelivery.Beam, 0, 0, heatPerSecond: 200_000);
            gun.SetDataBlob(new ShipCombatValueDB(40_000, 1e12, 1.0) { Evasion = 0, Weapons = { hotBeam }, HeatCapacity_kJ = attackerRadiatorCapacity });
            s.Game.OrderHandler.HandleOrder(FleetOrder.AssignShip(red.Id, atkFleet, gun));

            var defFleet = FleetFactory.Create(s.StartingSystem, s.Faction.Id, "Passive Defender");
            for (int i = 0; i < 3; i++)
            {
                var d = ShipFactory.CreateShip(design, s.Faction, s.StartingBody, "D" + i);
                d.SetDataBlob(new ShipCombatValueDB(0, 300_000, 1.0) { Evasion = 0 }); // modest toughness, fires nothing back
                s.Game.OrderHandler.HandleOrder(FleetOrder.AssignShip(s.Faction.Id, defFleet, d));
            }

            CombatEngagement.StartEngagement(atkFleet, defFleet);
            int steps = 0;
            while (defFleet.HasDataBlob<FleetCombatStateDB>() && steps < 60)
            {
                CombatEngagement.StepEngagement(atkFleet, defFleet, 5);
                steps++;
            }
            return CombatEngagement.GetFleetShips(defFleet).Count;
        }

        [Test]
        [Description("W5b: heat throttling decides a fight. An attacker firing ONLY a HOT energy weapon with a BIG radiator sheds the heat and sustains full fire, grinding the passive defender down; the SAME attacker with a SMALL radiator overheats after the first salvo, throttles toward the floor, and does far less — so MORE of the defender survives. The radiator is the only variable, so heat throttling is the difference (the burst-vs-sustained decision).")]
        public void HeatThrottling_ChokesAnUnderCooledBeamer_SoTheDefenderSurvives()
        {
            var s = TestScenario.CreateWithColony();
            var red = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);

            int defLeft_bigRad = RunHeatBattle(s, red, attackerRadiatorCapacity: 5_000_000);  // sheds the heat, sustains
            int defLeft_smallRad = RunHeatBattle(s, red, attackerRadiatorCapacity: 100_000);   // overheats, throttles

            Log($"defender survivors (of 3): attacker-big-radiator={defLeft_bigRad}, attacker-small-radiator={defLeft_smallRad}");
            Assert.That(defLeft_bigRad, Is.LessThan(3), "the well-cooled beamer sustains full fire and grinds the defender down (real losses)");
            Assert.That(defLeft_smallRad, Is.GreaterThan(defLeft_bigRad),
                "the under-cooled beamer overheats and throttles, so MORE of the defender survives — heat is the difference");
        }
    }
}
