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
        [Description("WarheadEnergyJoules safe default: a null ordnance reads 0 energy (so PickedWarheadFirepower "
                     + "falls back to the stub). Defensive — Calculate must never throw.")]
        public void WarheadEnergyJoules_NullOrdnance_ReadsZero()
        {
            Assert.That(ShipCombatValueDB.WarheadEnergyJoules(null), Is.EqualTo(0.0).Within(1e-9));
        }

        [Test]
        [Description("Byte-identity + the wire: a freshly-built missile ship rates the SAME firepower with the guided-"
                     + "warhead flag OFF and ON — because a just-built launcher has NOTHING LOADED (AssignedOrdnance is "
                     + "null), the ON path falls back to the stub. Proves the flag branch is exercised in Calculate AND "
                     + "that turning the flag on can never move a rating until the player actually loads a warhead.")]
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

            // A freshly-built launcher has no picked ordnance (AssignedOrdnance is null), so the ON read falls back
            // to the stub — regardless of what the faction has designed. (The harness start faction also designs no
            // ordnance, which is why nothing could be loaded to begin with.)
            Assert.That(factionInfo.MissileDesigns.Count, Is.EqualTo(0),
                "precondition: the harness start faction designs no ordnance — and a just-built ship loads none — so flag ON == flag OFF here.");

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

        [Test]
        [Description("THE PICKED WARHEAD (developer ruling 2026-08-17): a missile launcher's firepower reads the ordnance "
                     + "the player actually LOADED (MissileLauncherAtb.AssignedOrdnance), not the faction's heaviest. "
                     + "Load a warhead + recompute -> firepower rises above the flat stub, and a 4x warhead reads "
                     + "proportionally harder (the missile contribution scales with the picked warhead).")]
        public void PickedWarhead_DrivesFirepower_AndScalesWithTheWarhead()
        {
            var s = TestScenario.CreateWithColony();
            var factionInfo = s.Faction.GetDataBlob<FactionInfoDB>();

            // A start design that carries a missile launcher (robust — no hardcoded id).
            var missileDesign = factionInfo.ShipDesigns.Values.FirstOrDefault(d =>
            {
                var probe = ShipFactory.CreateShip(d, s.Faction, s.StartingBody, "probe-" + d.Name);
                return probe.TryGetDataBlob<ComponentInstancesDB>(out var comps)
                       && comps.TryGetComponentsByAttribute<MissileLauncherAtb>(out _);
            });
            Assert.That(missileDesign, Is.Not.Null, "no start design carries a missile launcher.");

            // A hand-built ordnance (the internal [JsonConstructor] + public Components list), NOT registered on the
            // faction — a single explosive-payload part of the given TNT-equivalent mass (kg).
            OrdnanceDesign Torpedo(double tntKg)
            {
                var part = new ComponentDesign { UniqueID = "test-warhead-" + tntKg, Name = tntKg + "kg Warhead" };
                part.AttributesByType[typeof(OrdnanceExplosivePayload)] =
                    new OrdnanceExplosivePayload(0, tntKg, tntKg, 0, 0, 0);   // (trigger=Contact, totalMass, tntEqMass, frag…)
                return new OrdnanceDesign
                {
                    Name = "Torpedo " + tntKg,
                    Components = new System.Collections.Generic.List<(ComponentDesign, int)> { (part, 1) }
                };
            }

            bool saved = ShipCombatValueDB.EnableGuidedWarheadFirepower;
            try
            {
                // Baseline: flag OFF -> every launcher rates the flat stub.
                ShipCombatValueDB.EnableGuidedWarheadFirepower = false;
                var ship = ShipFactory.CreateShip(missileDesign, s.Faction, s.StartingBody, "torpedo-boat");
                double fpStub = ShipCombatValueDB.Calculate(ship).Firepower;

                Assert.That(ship.TryGetDataBlob<ComponentInstancesDB>(out var comps), Is.True);
                Assert.That(comps.TryGetComponentsByAttribute<MissileLauncherAtb>(out var launchers), Is.True);
                int nLaunchers = launchers.Count;

                // Load the small warhead on every launcher (AssignedOrdnance is per-launcher-design) + recompute.
                double tntKg = 5.0;   // ~ a base-mod missile
                ShipCombatValueDB.EnableGuidedWarheadFirepower = true;
                foreach (var l in launchers) l.Design.GetAttribute<MissileLauncherAtb>().AssignOrdnance(Torpedo(tntKg));
                double fpSmall = ShipCombatValueDB.Calculate(ship).Firepower;

                // Now load a 4x warhead.
                foreach (var l in launchers) l.Design.GetAttribute<MissileLauncherAtb>().AssignOrdnance(Torpedo(4.0 * tntKg));
                double fpBig = ShipCombatValueDB.Calculate(ship).Firepower;

                Log($"launchers={nLaunchers}  fpStub={fpStub:0}  fpSmall={fpSmall:0}  fpBig={fpBig:0}");

                Assert.That(fpSmall, Is.GreaterThan(fpStub),
                    "the picked warhead reads STRONGER than the flat stub (the point of C-guided).");

                // The missile contribution scales linearly with the picked warhead: 5kg -> 20kg adds exactly
                // nLaunchers x (20-5)kg x TntJoulesPerKg / divisor of firepower (fresh ship -> health 1.0).
                double expectedDelta = nLaunchers * (3.0 * tntKg * ShipCombatValueDB.TntJoulesPerKg)
                                       / ShipCombatValueDB.GuidedWarheadDivisor;
                Assert.That(fpBig - fpSmall, Is.EqualTo(expectedDelta).Within(1.0),
                    "4x the picked warhead -> the missile firepower scales 4x (firepower reads the warhead you loaded).");
            }
            finally { ShipCombatValueDB.EnableGuidedWarheadFirepower = saved; }
        }
    }
}
