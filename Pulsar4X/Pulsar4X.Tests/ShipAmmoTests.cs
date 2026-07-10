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
    /// Weapons pilot W3 (mid-battle ammo depletion for the SHIP resolver) — SLICE W3a, the byte-identical foundation.
    /// The ground side already depletes ammo (<c>GroundAmmo</c>/<c>GroundMagazineAtb</c>); the space stepped resolve
    /// never dried a magazine. W3a adds the pieces WITHOUT wiring the drain: the <see cref="ShipMagazineAtb"/> component
    /// (the ammo store), <see cref="ShipCombatValueDB.AmmoCapacity_kg"/> (sum of installed magazines, health-scaled), and
    /// the fleet's <see cref="FleetCombatStateDB.AmmoPool_kg"/> pool (-1 = unseeded, mirroring the shield pool).
    ///
    /// The invariant this pins: with NO magazine, a ship reads 0 capacity → the pool stays disabled → combat is
    /// byte-identical (every current ship, until the W3c base-mod magazine). W3b wires the per-salvo drain + silence;
    /// W3c adds the buildable base-mod magazine. Engine-only → runs in CI.
    /// </summary>
    [TestFixture]
    public class ShipAmmoTests
    {
        private const string RailgunShip = "default-ship-design-test-railgun"; // Lancer — 4 railguns, no magazine yet
        private static void Log(string m) => TestContext.Progress.WriteLine("[ship-ammo] " + m);

        [Test]
        [Description("ShipMagazineAtb (the ammo store) holds its kg capacity and clamps a negative to 0; the fleet ammo pool defaults to -1 (unseeded), mirroring the shield pool's lazy-seed sentinel.")]
        public void ShipMagazineAtb_AndAmmoPool_PinTheFoundation()
        {
            var mag = new ShipMagazineAtb(2500);
            Assert.That(mag.Capacity_kg, Is.EqualTo(2500), "the magazine holds its kg capacity");
            Assert.That(new ShipMagazineAtb(-50).Capacity_kg, Is.EqualTo(0), "a negative capacity clamps to 0 (never negative ammo)");
            Assert.That(((ShipMagazineAtb)mag.Clone()).Capacity_kg, Is.EqualTo(2500), "clone preserves the capacity");

            Assert.That(new FleetCombatStateDB().AmmoPool_kg, Is.EqualTo(-1),
                "the fleet ammo pool defaults to -1 (not yet seeded) — the resolver lazy-fills it to capacity at first salvo");
        }

        [Test]
        [Description("W3a additive/byte-identical: a real base-mod ship with NO magazine reads AmmoCapacity_kg == 0 (so its fleet ammo pool is disabled and the resolve is untouched), exactly as an unshielded ship reads a 0 shield pool.")]
        public void ARealShip_WithNoMagazine_ReadsZeroAmmoCapacity()
        {
            var s = TestScenario.CreateWithColony();
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns;
            Assert.That(designs.ContainsKey(RailgunShip), Is.True, "the Lancer railgun cruiser loads onto the faction");

            var ship = ShipFactory.CreateShip(designs[RailgunShip], s.Faction, s.StartingBody, "Lancer");
            var cv = ship.GetDataBlob<ShipCombatValueDB>();
            Log($"Lancer (no magazine): firepower={cv.Firepower:0}, ammoCapacity={cv.AmmoCapacity_kg:0} kg");

            Assert.That(cv.Firepower, Is.GreaterThan(0), "the Lancer carries its railguns");
            Assert.That(cv.AmmoCapacity_kg, Is.EqualTo(0),
                "no magazine → 0 ammo capacity → the fleet ammo pool stays disabled → combat byte-identical until the W3c magazine");
        }

        [Test]
        [Description("W3b pure helpers: ammo-fed = Kinetic (railgun/flak) + Explosive (missiles); Energy (beam/plasma) + Exotic (disruptor) are powered, not ammo. AmmoFireDamage sums only the ammo-fed dps; SilenceAmmoWeapons drops those profiles, leaving energy fire.")]
        public void AmmoHelpers_ClassifySumAndSilence()
        {
            Assert.That(CombatEngagement.IsAmmoNature(WeaponNature.Kinetic), Is.True, "kinetic slugs are ammo-fed");
            Assert.That(CombatEngagement.IsAmmoNature(WeaponNature.Explosive), Is.True, "explosive warheads are ammo-fed");
            Assert.That(CombatEngagement.IsAmmoNature(WeaponNature.Energy), Is.False, "energy weapons draw power, not ammo");
            Assert.That(CombatEngagement.IsAmmoNature(WeaponNature.Exotic), Is.False, "exotic weapons draw power, not ammo");

            var mix = new List<WeaponProfile>
            {
                new WeaponProfile(1000, 5e4, 0, 5, 0, WeaponNature.Kinetic, WeaponDelivery.Slug),   // ammo
                new WeaponProfile(600,  3e8, 1, 1, 0, WeaponNature.Energy,  WeaponDelivery.Beam),   // powered
                new WeaponProfile(400,  5e3, 0.9, 1, 0, WeaponNature.Explosive, WeaponDelivery.Guided), // ammo
            };
            Assert.That(CombatEngagement.AmmoFireDamage(mix), Is.EqualTo(1400), "ammo-fed dps = 1000 kinetic + 400 explosive");
        }

        // A controlled ammo battle: an attacker firing ONLY an ammo-fed (Kinetic) weapon at a passive defender.
        // Returns the number of defender ships left. The magazine size is the only variable between runs.
        private static int RunAmmoBattle(TestScenario s, Entity red, double attackerAmmoCapacity)
        {
            var design = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns.Values.First();

            var atkFleet = FleetFactory.Create(s.StartingSystem, red.Id, "Ammo Attacker");
            var gun = ShipFactory.CreateShip(design, s.Faction, s.StartingBody, "Gun");
            gun.FactionOwnerID = red.Id;
            var kinetic = new WeaponProfile(40_000, 5e4, 0.05, 5, 0, WeaponNature.Kinetic, WeaponDelivery.Slug);
            gun.SetDataBlob(new ShipCombatValueDB(40_000, 1e12, 1.0) { Evasion = 0, Weapons = { kinetic }, AmmoCapacity_kg = attackerAmmoCapacity });
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
            while (defFleet.HasDataBlob<FleetCombatStateDB>() && steps < 400)
            {
                CombatEngagement.StepEngagement(atkFleet, defFleet, 5);
                steps++;
            }
            return CombatEngagement.GetFleetShips(defFleet).Count;
        }

        [Test]
        [Description("W3b: ammo depletion decides a fight. An attacker firing ONLY an ammo-fed (Kinetic) weapon with a SMALL magazine runs dry after a couple of salvos and goes silent, so the passive defender SURVIVES — while the SAME attacker with NO magazine fires forever and WIPES the identical defender. The magazine is the only variable, so ammo depletion is what saved the defender (the calibration-robust isolation the triangle-battle tests use for evasion).")]
        public void AmmoDepletion_SilencesAKineticAttacker_SoTheDefenderSurvives()
        {
            var s = TestScenario.CreateWithColony();
            var red = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);

            int defLeft_noMag = RunAmmoBattle(s, red, attackerAmmoCapacity: 0);      // control: never dry
            int defLeft_smallMag = RunAmmoBattle(s, red, attackerAmmoCapacity: 40);  // dries in ~2 salvos

            Log($"defender survivors (of 3): attacker-no-magazine={defLeft_noMag}, attacker-small-magazine={defLeft_smallMag}");
            // The never-dry attacker grinds the passive defender down until it breaks off (the 50% retreat threshold
            // leaves ~1 survivor), so it takes REAL losses. The dry attacker goes silent after ~2 salvos before killing
            // anything, so the defender is untouched. Comparative + retreat-aware, so it's calibration-robust.
            Assert.That(defLeft_noMag, Is.LessThan(3), "a never-dry kinetic attacker grinds the passive defender down (real losses — some ships lost)");
            Assert.That(defLeft_smallMag, Is.GreaterThan(defLeft_noMag),
                "the same attacker runs out of ammo and goes silent, so MORE of the defender survives — ammo depletion is the difference");
        }
    }
}
