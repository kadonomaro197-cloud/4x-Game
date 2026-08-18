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
    /// E14 — AURAS, slice 3a (Fork B, the FLEET-WIDE command buff). The developer's call: an aura projector mounted
    /// anywhere in a fleet buffs the WHOLE fleet's firepower/toughness — NOT a per-ship radius field (an earlier
    /// slice's per-ship sweep was superseded by this). Wired as one fold in <see cref="CombatEngagement.GetCombatShips"/>:
    /// the fleet-wide multiplier now also reads <see cref="CombatEngagement.FleetAuraMult"/> (Command→Firepower,
    /// Ward→Toughness, strongest projector wins), exactly beside the flagship-commander multiplier. Flag-gated
    /// (<see cref="CombatEngagement.EnableAuraCommandBuff"/>) default OFF → byte-identical. Engine-only → CI.
    /// </summary>
    [TestFixture]
    public class AuraCommandBuffTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[aura-command] " + m);

        [TearDown]
        public void ResetFlag() => CombatEngagement.EnableAuraCommandBuff = false;

        private static Entity Spawn(TestScenario s, ShipDesign d, string name)
            => ShipFactory.CreateShip(d, s.Faction, s.StartingBody, name);

        private static void Assign(TestScenario s, Entity fleet, Entity ship)
            => s.Game.OrderHandler.HandleOrder(FleetOrder.AssignShip(s.Faction.Id, fleet, ship));

        private static void GiveProjector(Entity ship, double magnitude,
            AuraEffect effect = AuraEffect.Command, AuraTarget target = AuraTarget.Friends, string name = "aura")
        {
            if (!ship.TryGetDataBlob<AuraProjectorDB>(out var roster))
            {
                roster = new AuraProjectorDB();
                ship.SetDataBlob(roster);
            }
            roster.Projectors.Add(new AuraProjectorField
            {
                Radius_m = 1000, Magnitude = magnitude, Effect = effect, Target = target, ComponentName = name,
            });
        }

        private static double Fp(Entity fleet, Entity ship)
            => CombatEngagement.GetCombatShips(fleet).First(cs => cs.Ship.Id == ship.Id).FirepowerMult;

        private static double Tough(Entity fleet, Entity ship)
            => CombatEngagement.GetCombatShips(fleet).First(cs => cs.Ship.Id == ship.Id).ToughnessMult;

        /// <summary>Build a fleet with a flagship (carries the aura) + a wingman (the one we measure the fleet-wide
        /// effect on).</summary>
        private static (TestScenario s, Entity fleet, Entity flagship, Entity wing) Group()
        {
            var s = TestScenario.CreateWithColony();
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns.Values.ToList();
            Assert.That(designs, Is.Not.Empty, "the start faction has ship designs to spawn");
            var fleet = FleetFactory.Create(s.StartingSystem, s.Faction.Id, "Aura Group");
            var flagship = Spawn(s, designs[0], "Flagship");
            var wing = Spawn(s, designs[0], "Wingman");
            Assign(s, fleet, flagship);
            Assign(s, fleet, wing);
            return (s, fleet, flagship, wing);
        }

        [Test]
        [Description("A Command aura on ONE ship raises the WHOLE fleet's firepower multiplier (the wingman too); "
                     + "flag OFF → byte-identical even with a projector present.")]
        public void CommandAura_RaisesFleetFirepower_FlagOffByteIdentical()
        {
            var (s, fleet, flagship, wing) = Group();

            CombatEngagement.EnableAuraCommandBuff = false;
            double baseFp = Fp(fleet, wing);

            GiveProjector(flagship, 0.5, AuraEffect.Command);   // a projector on the flagship only

            CombatEngagement.EnableAuraCommandBuff = false;
            Assert.That(Fp(fleet, wing), Is.EqualTo(baseFp).Within(1e-9), "flag off: byte-identical with a projector present");

            CombatEngagement.EnableAuraCommandBuff = true;
            Assert.That(Fp(fleet, wing), Is.EqualTo(baseFp * 1.5).Within(1e-9),
                "a Command aura anywhere in the fleet buffs the wingman fleet-wide (+50%)");
            Log($"fleet firepower {baseFp:0.###} → {Fp(fleet, wing):0.###} with a 0.5 Command aura");
        }

        [Test]
        [Description("A Ward aura raises the fleet's toughness multiplier fleet-wide.")]
        public void WardAura_RaisesFleetToughness()
        {
            var (s, fleet, flagship, wing) = Group();
            CombatEngagement.EnableAuraCommandBuff = false;
            double baseTough = Tough(fleet, wing);

            GiveProjector(flagship, 0.25, AuraEffect.Ward);
            CombatEngagement.EnableAuraCommandBuff = true;
            Assert.That(Tough(fleet, wing), Is.EqualTo(baseTough * 1.25).Within(1e-9), "a Ward aura toughens the fleet (+25%)");
            // A Ward aura does NOT touch firepower.
            CombatEngagement.EnableAuraCommandBuff = false;
            double baseFp = Fp(fleet, wing);
            CombatEngagement.EnableAuraCommandBuff = true;
            Assert.That(Fp(fleet, wing), Is.EqualTo(baseFp).Within(1e-9), "a Ward aura leaves firepower unchanged");
        }

        [Test]
        [Description("Overlapping auras take the BEST, never the SUM.")]
        public void OverlappingAuras_TakeTheBest_NotTheSum()
        {
            var (s, fleet, flagship, wing) = Group();
            CombatEngagement.EnableAuraCommandBuff = false;
            double baseFp = Fp(fleet, wing);

            GiveProjector(flagship, 0.2, AuraEffect.Command, name: "weak");
            GiveProjector(wing, 0.5, AuraEffect.Command, name: "strong");   // a second projector, on the wingman

            CombatEngagement.EnableAuraCommandBuff = true;
            // best = 0.5 → ×1.5 ; a SUM would be (0.2+0.5)=0.7 → ×1.7.
            Assert.That(Fp(fleet, wing), Is.EqualTo(baseFp * 1.5).Within(1e-9), "the strongest projector wins, never the sum");
        }

        [Test]
        [Description("Grave rung: removing the projector returns the fleet to its baseline multiplier.")]
        public void RemovedProjector_DropsTheBuff()
        {
            var (s, fleet, flagship, wing) = Group();
            CombatEngagement.EnableAuraCommandBuff = false;
            double baseFp = Fp(fleet, wing);

            GiveProjector(flagship, 0.5, AuraEffect.Command);
            CombatEngagement.EnableAuraCommandBuff = true;
            Assert.That(Fp(fleet, wing), Is.EqualTo(baseFp * 1.5).Within(1e-9), "buffed while the projector stands");

            flagship.RemoveDataBlob<AuraProjectorDB>();   // the projector is destroyed / uninstalled
            Assert.That(Fp(fleet, wing), Is.EqualTo(baseFp).Within(1e-9), "the buff is gone once the projector is (grave rung)");
        }

        [Test]
        [Description("A Foes-targeted field does NOT buff its own fleet.")]
        public void FoesAura_DoesNotBuffOwnFleet()
        {
            var (s, fleet, flagship, wing) = Group();
            CombatEngagement.EnableAuraCommandBuff = false;
            double baseFp = Fp(fleet, wing);

            GiveProjector(flagship, 0.5, AuraEffect.Command, AuraTarget.Foes);
            CombatEngagement.EnableAuraCommandBuff = true;
            Assert.That(Fp(fleet, wing), Is.EqualTo(baseFp).Within(1e-9), "a hostile-only field does not buff its own fleet");
        }
    }
}
