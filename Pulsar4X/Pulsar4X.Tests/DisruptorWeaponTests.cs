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
    /// Space SHIELD layer Phase D — the ANTI-SHIELD EXOTIC weapon (the Ion Disruptor) through the REAL data path,
    /// the exotic twin of <see cref="RailgunWeaponTests"/>. Builds the base-mod <c>default-ship-design-test-disruptor</c>
    /// (Ravager) the way the live game does (JSON <c>disruptor-weapon</c> template → <c>DisruptorWeaponAtb</c> via
    /// reflection → <see cref="ShipCombatValueDB"/>), so CI catches a template/ctor drift instead of the developer's
    /// New Game crashing (gotcha #10).
    ///
    /// The payoff it proves — the rock to the shield's scissors: the disruptor rates as a LIGHT-SPEED (undodgeable),
    /// EXOTIC-nature weapon, and through the real resolver it BYPASSES a shield pool — a shielded hull takes the same
    /// fire from it as an unshielded one, whereas an equal-power KINETIC gun is soaked by that same shield. That closes
    /// the anti-shield exotic cradle-to-grave (a researched → built → installed → losable weapon the shield can't stop).
    /// Engine-only → runs in CI.
    /// </summary>
    [TestFixture]
    public class DisruptorWeaponTests
    {
        private const string DisruptorShip = "default-ship-design-test-disruptor"; // Ravager — 3 × disruptor
        private static void Log(string m) => TestContext.Progress.WriteLine("[disruptor] " + m);

        /// <summary>A fleet holding one real base-mod ship built from JSON.</summary>
        private static Entity RealShipFleet(TestScenario s, Entity faction, string designId, string name)
        {
            var fleet = FleetFactory.Create(s.StartingSystem, faction.Id, name);
            var design = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns[designId];
            var ship = ShipFactory.CreateShip(design, s.Faction, s.StartingBody, name + " hull");
            ship.FactionOwnerID = faction.Id;
            s.Game.OrderHandler.HandleOrder(FleetOrder.AssignShip(faction.Id, fleet, ship));
            return fleet;
        }

        /// <summary>A fleet with one hand-built gun ship (fixed dps/nature, huge toughness, 0 evasion).</summary>
        private static Entity GunFleet(TestScenario s, Entity faction, WeaponProfile gun, string name)
        {
            var fleet = FleetFactory.Create(s.StartingSystem, faction.Id, name);
            var design = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns.Values.First();
            var ship = ShipFactory.CreateShip(design, s.Faction, s.StartingBody, name + " hull");
            ship.FactionOwnerID = faction.Id;
            ship.SetDataBlob(new ShipCombatValueDB(gun.DamagePerSecond, 1e15, 1.0) { Evasion = 0, Weapons = { gun } });
            s.Game.OrderHandler.HandleOrder(FleetOrder.AssignShip(faction.Id, fleet, ship));
            return fleet;
        }

        /// <summary>A single unkillable target (0 firepower, huge toughness), optionally shielded.</summary>
        private static Entity TargetFleet(TestScenario s, Entity faction, string name, double shieldCapacity)
        {
            var fleet = FleetFactory.Create(s.StartingSystem, faction.Id, name);
            var design = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns.Values.First();
            var ship = ShipFactory.CreateShip(design, s.Faction, s.StartingBody, name + " hull");
            ship.FactionOwnerID = faction.Id;
            ship.SetDataBlob(new ShipCombatValueDB(0, 1e15, 1.0) { Evasion = 0, ShieldCapacity_J = shieldCapacity, ShieldRegen_Jps = 0 });
            s.Game.OrderHandler.HandleOrder(FleetOrder.AssignShip(faction.Id, fleet, ship));
            return fleet;
        }

        /// <summary>Fire an attacker at a target for N salvos; return the damage that reached the target's HULL.</summary>
        private static double HullDamage(Entity attacker, Entity target, int salvos)
        {
            CombatEngagement.StartEngagement(attacker, target);
            for (int i = 0; i < salvos && target.HasDataBlob<FleetCombatStateDB>(); i++)
                CombatEngagement.StepEngagement(attacker, target, 5);
            return target.TryGetDataBlob<FleetCombatStateDB>(out var st) ? st.DamageTakenPool : double.NaN;
        }

        [Test]
        [Description("The base-mod Ion Disruptor builds from JSON and rates as a light-speed (undodgeable), exotic-nature weapon; through the real resolver it BYPASSES a shield pool (a shielded hull takes the same fire as an unshielded one), while an equal-power kinetic gun is soaked by that same shield.")]
        public void DisruptorDesign_BuildsRealComponent_AndBypassesShields()
        {
            var s = TestScenario.CreateWithColony();
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns;
            Assert.That(designs.ContainsKey(DisruptorShip), Is.True,
                "the Ravager ion frigate loads onto the faction — the JSON disruptor-weapon template + component + ship wired up");

            // Build it the real way (instantiates DisruptorWeaponAtb from JSON via reflection).
            var probe = ShipFactory.CreateShip(designs[DisruptorShip], s.Faction, s.StartingBody, "Ravager-probe");
            var cv = probe.GetDataBlob<ShipCombatValueDB>();
            var disruptor = cv.Weapons.FirstOrDefault(w => w.Nature == WeaponNature.Exotic);
            Log($"Ravager: firepower={cv.Firepower:0}, weapons={cv.Weapons.Count}, exotic profiles={cv.Weapons.Count(w => w.Nature == WeaponNature.Exotic)}");

            Assert.That(cv.Firepower, Is.GreaterThan(0), "the Ravager is armed (disruptor firepower flows into the combat value)");
            Assert.That(disruptor, Is.Not.Null, "an Ion Disruptor produced an EXOTIC-nature weapon profile — JSON template → DisruptorWeaponAtb → combat value is wired");
            Assert.That(disruptor.Velocity, Is.EqualTo(ShipCombatValueDB.LightSpeed_mps).Within(1), "light-speed delivery");
            // Undodgeable: even a maxed-out dodger can't evade a light-speed lance.
            Assert.That(CombatEngagement.HitFraction(disruptor, 0.95), Is.GreaterThan(0.95),
                "light-speed → the disruptor can't be dodged (a beam-class delivery)");

            const int salvos = 10;
            const double capacity = 5_000_000;

            // Two identical Ravagers vs a shielded and an unshielded target — the shield must make NO difference.
            var red = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);
            double shieldedVsDisruptor = HullDamage(RealShipFleet(s, red, DisruptorShip, "RavagerA"),
                                                    TargetFleet(s, s.Faction, "ShieldedA", capacity), salvos);
            double unshieldedVsDisruptor = HullDamage(RealShipFleet(s, red, DisruptorShip, "RavagerB"),
                                                      TargetFleet(s, s.Faction, "Unshielded", 0), salvos);

            // A KINETIC gun of the SAME firepower vs the SAME shield — the shield SHOULD soak this.
            var kineticGun = new WeaponProfile(cv.Firepower, 50_000, 0.05, 5, 0, WeaponNature.Kinetic, WeaponDelivery.Slug);
            double shieldedVsKinetic = HullDamage(GunFleet(s, red, kineticGun, "Slugger"),
                                                  TargetFleet(s, s.Faction, "ShieldedB", capacity), salvos);

            Log($"hull damage after {salvos} salvos — shielded-vs-disruptor={shieldedVsDisruptor:0}, unshielded-vs-disruptor={unshieldedVsDisruptor:0}, shielded-vs-kinetic={shieldedVsKinetic:0}");

            Assert.That(shieldedVsDisruptor, Is.EqualTo(unshieldedVsDisruptor).Within(unshieldedVsDisruptor * 0.001),
                "the disruptor's exotic nature BYPASSES the shield — a shielded hull takes the same fire as an unshielded one");
            Assert.That(shieldedVsKinetic, Is.LessThan(shieldedVsDisruptor),
                "the same shield SOAKS an equal-power kinetic gun — so far more disruptor fire lands than kinetic fire (the anti-shield payoff)");
        }
    }
}
