using System.Linq;
using NUnit.Framework;
using Pulsar4X.Combat;
using Pulsar4X.Components;
using Pulsar4X.Datablobs;
using Pulsar4X.Factions;
using Pulsar4X.Ships;
using Pulsar4X.Weapons;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// C-GUIDED — a missile launcher's auto-resolve firepower reads its REAL warhead (OPERATION BLUEPRINT-TO-STEEL,
    /// entityassembler.html TIER 3 #3, developer ruling "Option A"). The auto-resolver rated every missile launcher
    /// at a flat 100 kJ/s stub, so torpedo ships read far weaker than they are. Now, when
    /// <see cref="ShipCombatValueDB.EnableGuidedWarheadFirepower"/> is on, a launcher's firepower is read from a
    /// REPRESENTATIVE warhead — the heaviest ordnance in the owning faction's library it can load — scaled to a
    /// firepower by <see cref="ShipCombatValueDB.GuidedWarheadDivisor"/>.
    ///
    /// <para><b>Flag-gated + byte-identical by default.</b> The flag defaults OFF (the client turns it on), and the
    /// divisor is a live-tuned calibration (missile TNT energy is MJ-scale — a real warhead reads several times the
    /// old stub). These gauges pin: (1) the pure warhead→firepower scaling and its stub fallback; (2) the SAFETY —
    /// building a real missile ship on the start faction (which has designed no ordnance) rates the SAME firepower
    /// flag-OFF and flag-ON, because the ON path falls back to the stub. So turning the flag on can never change a
    /// rating unless the faction actually has a warhead to read.</para>
    /// </summary>
    [TestFixture]
    public class GuidedWarheadFirepowerTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[c-guided] " + m);

        [Test]
        [Description("The warhead->firepower map: no warhead (energy 0) keeps the flat stub; a real warhead scales "
                     + "linearly by the divisor and reads STRONGER than the stub; 4x the warhead -> 4x the firepower.")]
        public void WarheadFirepower_ScalesWithTheWarhead_FallsBackToStubWhenNone()
        {
            double stub = ShipCombatValueDB.MissileLauncherFirepowerStub;
            double div = ShipCombatValueDB.GuidedWarheadDivisor;

            Assert.That(ShipCombatValueDB.WarheadFirepower(0.0), Is.EqualTo(stub).Within(1e-9),
                "no explosive warhead to represent -> the flat stub (a launcher with nothing to fire still rates something).");

            // A base-mod ~5 kg-TNT warhead ~ 5 * 4.184e6 = 2.092e7 J.
            double smallWarhead = 5.0 * ShipCombatValueDB.TntJoulesPerKg;
            double bigWarhead = 4.0 * smallWarhead;

            Assert.That(ShipCombatValueDB.WarheadFirepower(smallWarhead), Is.EqualTo(smallWarhead / div).Within(1e-6),
                "firepower = warhead energy / divisor.");
            Assert.That(ShipCombatValueDB.WarheadFirepower(smallWarhead), Is.GreaterThan(stub),
                "a real warhead reads STRONGER than the old flat stub (the design's complaint).");
            Assert.That(ShipCombatValueDB.WarheadFirepower(bigWarhead),
                Is.EqualTo(4.0 * ShipCombatValueDB.WarheadFirepower(smallWarhead)).Within(1e-3),
                "4x the warhead -> 4x the firepower (scales with the warhead chosen).");

            Log($"stub={stub:0}  small-warhead-fp={ShipCombatValueDB.WarheadFirepower(smallWarhead):0}  big={ShipCombatValueDB.WarheadFirepower(bigWarhead):0}");
        }

        [Test]
        [Description("WarheadEnergyJoules safe default: a null ordnance reads 0 energy (so RepresentativeLauncherFirepower "
                     + "falls back to the stub). Defensive — Calculate must never throw.")]
        public void WarheadEnergyJoules_NullOrdnance_ReadsZero()
        {
            Assert.That(ShipCombatValueDB.WarheadEnergyJoules(null), Is.EqualTo(0.0).Within(1e-9));
        }

        [Test]
        [Description("Byte-identity + the wire: a real missile ship built on the start faction rates the SAME firepower "
                     + "with the guided-warhead flag OFF and ON — because the start faction has designed no ordnance, the "
                     + "ON path falls back to the stub. Proves the flag branch is exercised in Calculate AND that turning "
                     + "the flag on can never move a rating unless the faction actually has a warhead to read.")]
        public void MissileShip_FlagOnWithNoOrdnance_IsByteIdenticalToStub()
        {
            var s = TestScenario.CreateWithColony();
            var factionInfo = s.Faction.GetDataBlob<FactionInfoDB>();

            // Find a start design that carries a missile launcher (robust — no hardcoded design id).
            var missileDesign = factionInfo.ShipDesigns.Values.FirstOrDefault(d =>
            {
                var probe = ShipFactory.CreateShip(d, s.Faction, s.StartingBody, "probe-" + d.Name);
                return probe.TryGetDataBlob<ComponentInstancesDB>(out var comps)
                       && comps.TryGetComponentsByAttribute<MissileLauncherAtb>(out _);
            });
            Assert.That(missileDesign, Is.Not.Null,
                "no start design carries a missile launcher — the base-mod missile ship this gauge needs is missing.");

            // The start faction (CreateBasicFaction) designs no ordnance, so the ON path must fall back to the stub.
            Assert.That(factionInfo.MissileDesigns.Count, Is.EqualTo(0),
                "precondition: the harness start faction has designed no ordnance, so flag ON must equal flag OFF here.");

            bool saved = ShipCombatValueDB.EnableGuidedWarheadFirepower;
            try
            {
                ShipCombatValueDB.EnableGuidedWarheadFirepower = false;
                var shipOff = ShipFactory.CreateShip(missileDesign, s.Faction, s.StartingBody, "guided-off");
                Assert.That(shipOff.TryGetDataBlob<ShipCombatValueDB>(out var cvOff), Is.True);

                ShipCombatValueDB.EnableGuidedWarheadFirepower = true;
                var shipOn = ShipFactory.CreateShip(missileDesign, s.Faction, s.StartingBody, "guided-on");
                Assert.That(shipOn.TryGetDataBlob<ShipCombatValueDB>(out var cvOn), Is.True);

                Log($"{missileDesign.Name}: firepower off={cvOff.Firepower:0.###}  on={cvOn.Firepower:0.###}  (faction ordnance designs={factionInfo.MissileDesigns.Count})");

                Assert.That(cvOff.Firepower, Is.GreaterThan(0),
                    "the missile ship rates firepower from its launcher stub.");
                Assert.That(cvOn.Firepower, Is.EqualTo(cvOff.Firepower).Within(1e-6),
                    "with no ordnance to read, flag ON == flag OFF (byte-identical fallback — the safety guarantee).");
            }
            finally { ShipCombatValueDB.EnableGuidedWarheadFirepower = saved; }
        }
    }
}
