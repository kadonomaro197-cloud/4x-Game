using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pulsar4X.Combat;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Movement;
using Pulsar4X.Orbital;
using Pulsar4X.Ships;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// E14 — AURAS, slice 2 (the SWEEP). Gauges the per-tick pass (<see cref="AuraSweepProcessor"/>) that takes a
    /// projector's field to the ships around it + the install/grave-rung hooks (<see cref="AuraAtb"/> ↔
    /// <see cref="AuraProjectorDB"/>). Real (valid) ship entities, positioned by hand; the sweep is driven directly
    /// (the colony harness does not reliably auto-fire hotloops — the SpaceHazardTests idiom). Engine-only → CI.
    ///
    /// Byte-identical in a live game: nothing reads <see cref="AuraBuffDB"/> yet (the combat-read is the next slice),
    /// no base-mod component mounts an aura, and the sweep is gated behind <see cref="AuraSweepProcessor.EnableAuraSweep"/>.
    /// </summary>
    [TestFixture]
    public class AuraSweepTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[aura-sweep] " + m);

        [TearDown]
        public void ResetFlag() => AuraSweepProcessor.EnableAuraSweep = false;

        /// <summary>Build a real ship and place it at a chosen ABSOLUTE position (optionally under another faction).</summary>
        private static Entity ShipAt(TestScenario s, Vector3 abs, string name, int? factionId = null)
        {
            var design = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns.Values.First();
            var ship = ShipFactory.CreateShip(design, s.Faction, s.StartingBody, name);
            ship.GetDataBlob<PositionDB>().AbsolutePosition = abs;
            if (factionId.HasValue)
                ship.FactionOwnerID = factionId.Value;
            return ship;
        }

        private static void GiveProjector(Entity ship, double radius, double magnitude,
            AuraEffect effect = AuraEffect.Command, AuraTarget target = AuraTarget.Friends, string name = "aura")
        {
            ship.SetDataBlob(new AuraProjectorDB
            {
                Projectors = new List<AuraProjectorField>
                {
                    new AuraProjectorField
                    {
                        Radius_m = radius, Magnitude = magnitude,
                        Effect = effect, Target = target, ComponentName = name,
                    }
                }
            });
        }

        private static double Firepower(Entity ship)
            => ship.TryGetDataBlob<AuraBuffDB>(out var b) ? b.Firepower : 0.0;

        [Test]
        [Description("A friendly Command aura buffs a friendly ship IN range (distance-tapered) and reaches NOTHING "
                     + "beyond its radius.")]
        public void CommandAura_BuffsFriendlyInRange_TaperedByDistance_NoneBeyond()
        {
            var s = TestScenario.CreateWithColony();
            const double R = 1000.0, M = 0.4;

            var projector = ShipAt(s, new Vector3(0, 0, 0), "Projector");
            GiveProjector(projector, R, M);                                   // Command / Friends
            var near = ShipAt(s, new Vector3(500, 0, 0), "FriendNear");       // dist 500 → taper 0.5
            var far = ShipAt(s, new Vector3(2000, 0, 0), "FriendFar");        // dist 2000 → out of range

            AuraSweepProcessor.EnableAuraSweep = true;
            new AuraSweepProcessor().ProcessManager(s.StartingSystem, 5);

            Assert.That(Firepower(near), Is.EqualTo(M * 0.5).Within(1e-9), "half-radius friend → half-strength buff");
            Assert.That(near.HasDataBlob<AuraBuffDB>(), Is.True, "the in-range friend carries the buff record");
            Assert.That(far.HasDataBlob<AuraBuffDB>(), Is.False, "a ship beyond the radius gets no buff at all");
            Log($"near firepower {Firepower(near):0.###} (expected {M * 0.5:0.###}); far unbuffed");
        }

        [Test]
        [Description("IFF: a Friends aura does NOT touch an in-range ship of another faction.")]
        public void FriendsAura_DoesNotBuffAnEnemy()
        {
            var s = TestScenario.CreateWithColony();
            const double R = 1000.0, M = 0.4;

            var projector = ShipAt(s, new Vector3(0, 0, 0), "Projector");
            GiveProjector(projector, R, M, AuraEffect.Command, AuraTarget.Friends);
            var enemy = ShipAt(s, new Vector3(300, 0, 0), "Enemy", factionId: 999); // in range, wrong faction

            AuraSweepProcessor.EnableAuraSweep = true;
            new AuraSweepProcessor().ProcessManager(s.StartingSystem, 5);

            Assert.That(enemy.HasDataBlob<AuraBuffDB>(), Is.False, "a Friends aura is invisible to an enemy in range");
        }

        [Test]
        [Description("Overlapping auras take the BEST, never the SUM — two projectors over one ship → the strongest "
                     + "single field wins.")]
        public void OverlappingAuras_TakeTheBest_NotTheSum()
        {
            var s = TestScenario.CreateWithColony();
            const double R = 1000.0;

            var weak = ShipAt(s, new Vector3(0, 0, 0), "Weak");
            GiveProjector(weak, R, 0.2, name: "weak");                        // M 0.2
            var strong = ShipAt(s, new Vector3(0, 0, 0), "Strong");
            GiveProjector(strong, R, 0.4, name: "strong");                    // M 0.4 (co-located projector)
            var target = ShipAt(s, new Vector3(500, 0, 0), "Target");         // dist 500 → taper 0.5

            AuraSweepProcessor.EnableAuraSweep = true;
            new AuraSweepProcessor().ProcessManager(s.StartingSystem, 5);

            // best = 0.4 × 0.5 = 0.2 ; a SUM would be (0.2+0.4) × 0.5 = 0.3.
            Assert.That(Firepower(target), Is.EqualTo(0.4 * 0.5).Within(1e-9), "the strongest field wins, never the sum");
        }

        [Test]
        [Description("Grave rung: once the projector's field is gone, the next sweep drops the ship's buff record.")]
        public void DestroyedProjector_DropsTheBuff_NextSweep()
        {
            var s = TestScenario.CreateWithColony();
            const double R = 1000.0, M = 0.4;

            var projector = ShipAt(s, new Vector3(0, 0, 0), "Projector");
            GiveProjector(projector, R, M);
            var near = ShipAt(s, new Vector3(500, 0, 0), "FriendNear");

            AuraSweepProcessor.EnableAuraSweep = true;
            var proc = new AuraSweepProcessor();
            proc.ProcessManager(s.StartingSystem, 5);
            Assert.That(near.HasDataBlob<AuraBuffDB>(), Is.True, "buffed while the projector stands");

            // The projector is destroyed / its component shot off → the marker goes.
            projector.RemoveDataBlob<AuraProjectorDB>();
            proc.ProcessManager(s.StartingSystem, 5);
            Assert.That(near.HasDataBlob<AuraBuffDB>(), Is.False, "the buff drops the sweep after the field is gone");
        }

        [Test]
        [Description("Flag OFF (default) → the sweep writes nothing (byte-identical).")]
        public void FlagOff_WritesNoBuff()
        {
            var s = TestScenario.CreateWithColony();
            var projector = ShipAt(s, new Vector3(0, 0, 0), "Projector");
            GiveProjector(projector, 1000.0, 0.4);
            var near = ShipAt(s, new Vector3(500, 0, 0), "FriendNear");

            AuraSweepProcessor.EnableAuraSweep = false; // the default
            new AuraSweepProcessor().ProcessManager(s.StartingSystem, 5);

            Assert.That(near.HasDataBlob<AuraBuffDB>(), Is.False, "flag off → no buff written anywhere");
        }

        [Test]
        [Description("The install hook snapshots the projector's field onto the host; the LAST uninstall drops the "
                     + "marker (the cradle-to-grave install/lose rung).")]
        public void InstallHook_AddsProjectorField_UninstallDropsIt()
        {
            var s = TestScenario.CreateWithColony();
            var host = ShipAt(s, new Vector3(0, 0, 0), "Host");

            var atb = new AuraAtb(500, 0.3, (double)(int)AuraEffect.Ward, (double)(int)AuraTarget.Friends);
            atb.OnComponentInstallation(host, null);

            Assert.That(host.TryGetDataBlob<AuraProjectorDB>(out var roster), Is.True, "install seeds the marker");
            Assert.That(roster.Projectors.Count, Is.EqualTo(1));
            Assert.That(roster.Projectors[0].Radius_m, Is.EqualTo(500));
            Assert.That(roster.Projectors[0].Effect, Is.EqualTo(AuraEffect.Ward));
            Assert.That(roster.Projectors[0].Target, Is.EqualTo(AuraTarget.Friends));

            atb.OnComponentUninstallation(host, null);
            Assert.That(host.HasDataBlob<AuraProjectorDB>(), Is.False, "the last projector torn down drops the marker (grave rung)");
        }
    }
}
