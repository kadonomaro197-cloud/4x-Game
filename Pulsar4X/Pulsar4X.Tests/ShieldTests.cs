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
    /// SPACE SHIELD layer (docs/WEAPON-TAXONOMY-DESIGN.md §6, developer's call 2026-07-06). The shield is the
    /// "shield" mechanism on the defence axis — a depleting + regenerating energy POOL (option B) that soaks
    /// incoming fire BEFORE the hull's toughness, with the weapon-NATURE matchup (Kinetic fully soaked, Energy
    /// half-bleeds, Exotic anti-shield bypasses).
    ///
    /// Phase A (built earlier): <see cref="ShieldAtb"/> carries the pool; <see cref="ShipCombatValueDB"/> sums it.
    /// Phase B (this pass): the resolver drains/regenerates the pool per salvo. Gauged three ways — the pure soak
    /// FRACTION math (<see cref="CombatEngagement.SoakFractionOf"/>), the pure pool DRAIN/REGEN math
    /// (<see cref="CombatEngagement.ResolveShield"/>), and END-TO-END through the real
    /// <see cref="CombatEngagement.StepEngagement"/> resolver (a shielded fleet's damage pool climbs slower under
    /// kinetic fire, and an exotic attacker erases that advantage). ADDITIVE — an unshielded ship reads a 0 pool,
    /// so combat is byte-identical until a shield is fitted. Engine-only → runs in CI.
    /// </summary>
    [TestFixture]
    public class ShieldTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[shield] " + m);

        [Test]
        [Description("Phase A: the ShieldAtb carries a depleting/regen pool (Capacity_J + RegenRate_Jps) and clamps negatives; ShipCombatValueDB carries the shield-pool fields defaulting to 0, so every ship with no generator reads 0 → combat unchanged.")]
        public void ShieldAtb_CarriesPool_AndCombatValueDefaultsToZero()
        {
            var gen = new ShieldAtb(capacity_J: 500000, regenRate_Jps: 10000);
            Assert.That(gen.Capacity_J, Is.EqualTo(500000), "the shield pool size");
            Assert.That(gen.RegenRate_Jps, Is.EqualTo(10000), "the recharge rate");

            var clamped = new ShieldAtb(-1, -1);
            Assert.That(clamped.Capacity_J, Is.EqualTo(0), "negative capacity clamps to 0");
            Assert.That(clamped.RegenRate_Jps, Is.EqualTo(0), "negative regen clamps to 0");

            var unshielded = new ShipCombatValueDB();
            Assert.That(unshielded.ShieldCapacity_J, Is.EqualTo(0), "an unshielded ship has a 0 pool — combat is byte-identical");
            Assert.That(unshielded.ShieldRegen_Jps, Is.EqualTo(0));
        }

        [Test]
        [Description("Phase B math #1 — SoakFractionOf rolls the nature matchup over a salvo: all-kinetic fully soakable (1.0), all-energy half (0.5), all-exotic none (0.0), and a 50/50 kinetic+exotic damage mix interpolates to 0.5.")]
        public void SoakFraction_RollsUpTheNatureMatchup()
        {
            WeaponProfile Gun(double dps, WeaponNature nat) => new WeaponProfile(dps, 1e6, 1, 1, 0, nat);

            Assert.That(CombatEngagement.SoakFractionOf(new List<WeaponProfile> { Gun(100, WeaponNature.Kinetic) }),
                Is.EqualTo(CombatEngagement.ShieldSoakVsKinetic), "kinetic is fully soakable");
            Assert.That(CombatEngagement.SoakFractionOf(new List<WeaponProfile> { Gun(100, WeaponNature.Energy) }),
                Is.EqualTo(CombatEngagement.ShieldSoakVsEnergy), "energy bleeds — only half soakable");
            Assert.That(CombatEngagement.SoakFractionOf(new List<WeaponProfile> { Gun(100, WeaponNature.Exotic) }),
                Is.EqualTo(CombatEngagement.ShieldSoakVsExotic), "exotic anti-shield bypasses");

            // equal kinetic + exotic damage → average of 1.0 and 0.0 = 0.5
            double mixed = CombatEngagement.SoakFractionOf(new List<WeaponProfile>
                { Gun(100, WeaponNature.Kinetic), Gun(100, WeaponNature.Exotic) });
            Assert.That(mixed, Is.EqualTo(0.5).Within(1e-9), "a 50/50 kinetic+exotic salvo half-soaks");
        }

        [Test]
        [Description("Phase B math #2 — ResolveShield drains the soakable part of a salvo up to the charge, bleeds the rest through, regenerates toward capacity, and is a no-op for an unshielded (0-capacity) pool.")]
        public void ResolveShield_DrainsRegensAndBypasses()
        {
            // Unshielded: 0 capacity absorbs nothing (byte-identical path).
            var (absU, poolU) = CombatEngagement.ResolveShield(pool: 0, capacity: 0, regen: 0, salvoDamage: 500, soakFraction: 1.0, dt: 5);
            Assert.That(absU, Is.EqualTo(0), "no generator → nothing absorbed");
            Assert.That(poolU, Is.EqualTo(0));

            // Kinetic salvo smaller than the charge: fully absorbed, pool drops.
            var (abs1, pool1) = CombatEngagement.ResolveShield(pool: 1000, capacity: 1000, regen: 0, salvoDamage: 300, soakFraction: 1.0, dt: 5);
            Assert.That(abs1, Is.EqualTo(300), "kinetic fully soaked while the shield holds");
            Assert.That(pool1, Is.EqualTo(700));

            // Salvo larger than the charge: absorbs only what's left, pool bottoms out.
            var (abs2, pool2) = CombatEngagement.ResolveShield(pool: 400, capacity: 1000, regen: 0, salvoDamage: 900, soakFraction: 1.0, dt: 5);
            Assert.That(abs2, Is.EqualTo(400), "can't absorb more than the remaining charge");
            Assert.That(pool2, Is.EqualTo(0), "shield collapses");

            // Exotic salvo (soakFraction 0): nothing absorbed even at full charge → bypass.
            var (abs3, pool3) = CombatEngagement.ResolveShield(pool: 1000, capacity: 1000, regen: 0, salvoDamage: 900, soakFraction: 0.0, dt: 5);
            Assert.That(abs3, Is.EqualTo(0), "exotic anti-shield passes straight through");
            Assert.That(pool3, Is.EqualTo(1000), "the shield isn't even touched");

            // Regen tops up toward capacity and clamps there.
            var (_, pool4) = CombatEngagement.ResolveShield(pool: 200, capacity: 1000, regen: 500, salvoDamage: 0, soakFraction: 0.0, dt: 5);
            Assert.That(pool4, Is.EqualTo(1000), "regen (500/s × 5s = 2500) clamps at capacity");
        }

        // --- end-to-end through the real resolver -----------------------------------------------------------------

        /// <summary>Assign a ship carrying the given combat value to a fresh fleet; returns the fleet.</summary>
        private static Entity FleetOf(TestScenario s, Entity faction, string name, ShipCombatValueDB cv)
        {
            var fleet = FleetFactory.Create(s.StartingSystem, faction.Id, name);
            var design = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns.Values.First();
            var ship = ShipFactory.CreateShip(design, s.Faction, s.StartingBody, name + " hull");
            ship.FactionOwnerID = faction.Id;
            ship.SetDataBlob(cv);
            s.Game.OrderHandler.HandleOrder(FleetOrder.AssignShip(faction.Id, fleet, ship));
            return fleet;
        }

        /// <summary>Run a one-gun attacker on a single unkillable target for K salvos and return the damage that
        /// reached the target's HULL (its accumulated DamageTakenPool). The target has huge toughness + 0 firepower,
        /// so the fight neither ends nor retreats — it's a pure "how much got through the shield" meter.</summary>
        private static double HullDamageAfter(TestScenario s, Entity attackerFaction, WeaponProfile gun,
            double shieldCapacity, double shieldRegen, int salvos)
        {
            var target = FleetOf(s, s.Faction, "Target",
                new ShipCombatValueDB(0, 1e15, 1.0) { Evasion = 0, ShieldCapacity_J = shieldCapacity, ShieldRegen_Jps = shieldRegen });
            var attacker = FleetOf(s, attackerFaction, "Attacker",
                new ShipCombatValueDB(gun.DamagePerSecond, 1e15, 1.0) { Evasion = 0, Weapons = { gun } });

            CombatEngagement.StartEngagement(attacker, target);
            for (int i = 0; i < salvos && target.HasDataBlob<FleetCombatStateDB>(); i++)
                CombatEngagement.StepEngagement(attacker, target, 5);

            return target.TryGetDataBlob<FleetCombatStateDB>(out var st) ? st.DamageTakenPool : double.NaN;
        }

        [Test]
        [Description("Phase B end-to-end — through the REAL resolver, a shielded fleet's hull takes LESS kinetic fire than an unshielded one (the pool soaks it), while an EXOTIC (anti-shield) attacker erases that advantage entirely — proving both the pool wiring and the nature matchup.")]
        public void Shield_InAFight_SoaksKinetic_ButExoticBypasses()
        {
            var s = TestScenario.CreateWithColony();
            var red = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);
            const double dps = 40_000;
            const double capacity = 5_000_000;   // big enough to matter over the run
            const int salvos = 20;

            // A kinetic slug (nature Kinetic) vs no shield, and vs the same shield.
            var kineticGun = new WeaponProfile(dps, 50_000, 0.05, 5, 0, WeaponNature.Kinetic, WeaponDelivery.Slug);
            double unshielded = HullDamageAfter(s, red, kineticGun, shieldCapacity: 0, shieldRegen: 0, salvos: salvos);
            double shieldedVsKinetic = HullDamageAfter(s, red, kineticGun, shieldCapacity: capacity, shieldRegen: 0, salvos: salvos);

            // The SAME shield vs an EXOTIC (anti-shield) attacker of the SAME dps — the shield should give no benefit.
            var exoticGun = new WeaponProfile(dps, 3e8, 0.95, 1, 0, WeaponNature.Exotic, WeaponDelivery.Beam);
            double shieldedVsExotic = HullDamageAfter(s, red, exoticGun, shieldCapacity: capacity, shieldRegen: 0, salvos: salvos);

            Log($"hull damage after {salvos} salvos — unshielded={unshielded:0}, shielded(kinetic)={shieldedVsKinetic:0}, shielded(exotic)={shieldedVsExotic:0}");

            Assert.That(shieldedVsKinetic, Is.LessThan(unshielded),
                "the shield soaks kinetic fire — less reaches the shielded hull than the unshielded one");
            Assert.That(shieldedVsExotic, Is.GreaterThan(shieldedVsKinetic),
                "an exotic anti-shield attacker bypasses the pool — more reaches the hull than the same-dps kinetic gun the shield stopped");
            Assert.That(shieldedVsExotic, Is.EqualTo(unshielded).Within(unshielded * 0.001),
                "vs exotic, the shield is as if it weren't there — the hull takes the same fire as the unshielded fleet");
        }
    }
}
