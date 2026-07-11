using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pulsar4X.Components;
using Pulsar4X.Datablobs;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Movement;
using Pulsar4X.Orbital;
using Pulsar4X.Sensors;
using Pulsar4X.Ships;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Sensors ⚙3 ▸ EW — BARRAGE JAMMING (S4). The offensive twin of the cloak: where a cloak hides YOU, a jammer
    /// blinds THE ENEMY — it floods the band so hostile sensors need a much stronger signal to resolve anything,
    /// shrinking how far off they detect you. The catch: an active jammer is a loud beacon (its own signature spikes),
    /// so you blind them but paint a target on yourself. It acts on the SAME detection substrate Detection uses — the
    /// barrage divides down the signal a hostile receiver gets in <see cref="SensorTools.GetDetectedEntites"/>, exactly
    /// as a hazard already degrades an observer's scan.
    ///
    /// Byte-identical for every current ship: gated behind <see cref="JammerAtb.EnableJamming"/> (default off, client-on),
    /// and inert until a jammer exists (no in-range hostile jammer → divisor 1.0 → no change). The new Havoc Escort
    /// Jammer is a new example ship (no fixture perturbed). Engine-only → CI.
    /// </summary>
    [TestFixture]
    public class ShipJammerTests
    {
        private const string Havoc = "default-ship-design-test-havoc";
        private static void Log(string m) => TestContext.Progress.WriteLine("[jammer] " + m);

        /// <summary>Build a ship from a design under the player faction, then stamp its true owner — the build-then-flip
        /// idiom the combat/detection fixtures use (CreateShip needs the player's unlocked data store).</summary>
        private static Entity BuildShip(TestScenario s, Entity owner, string designId, string name)
        {
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns;
            var design = designs.TryGetValue(designId, out var d) ? d : designs.Values.First();
            var ship = ShipFactory.CreateShip(design, s.Faction, s.StartingBody, name);
            ship.FactionOwnerID = owner.Id;
            return ship;
        }

        /// <summary>Fire the sensor scan once on every sensor-bearing entity — what Game.PostNewGameInitialization does
        /// at New Game, reproduced here because the harness doesn't (see SensorDetectionTests).</summary>
        private static void RunSensorScan(TestScenario s)
        {
            foreach (var entity in s.StartingSystem.GetAllEntitiesWithDataBlob<SensorAbilityDB>())
                s.Game.ProcessorManager.GetInstanceProcessor(nameof(SensorScan))
                    .ProcessEntity(entity, s.Game.TimePulse.GameGlobalDateTime);
        }

        /// <summary>The raw per-scan signal strength the watcher last picked up for a target (0 if not detected).</summary>
        private static double SignalFor(Entity watcher, Entity target)
        {
            var ability = watcher.GetDataBlob<SensorAbilityDB>();
            foreach (var (ent, ret) in ability.CurrentContacts)
                if (ent.Id == target.Id) return ret.SignalStrength_kW;
            return 0;
        }

        [Test]
        [Description("JammerAtb holds its dials and clamps them to honest ranges (degrade >= 1, range >= 0, boost >= 1 — a jammer can't help the enemy see or make you quieter); clone preserves them.")]
        public void JammerAtb_ClampsAndClones()
        {
            var j = new JammerAtb(4.0, 1e9, 5.0);
            Assert.That(j.SensitivityDegrade, Is.EqualTo(4.0));
            Assert.That(j.Range_m, Is.EqualTo(1e9));
            Assert.That(j.SelfSignatureBoost, Is.EqualTo(5.0));

            var floored = new JammerAtb(0.5, -1.0, 0.5);
            Assert.That(floored.SensitivityDegrade, Is.EqualTo(1.0), "degrade clamps up to 1 (never helps the enemy)");
            Assert.That(floored.Range_m, Is.EqualTo(0.0), "range clamps up to 0");
            Assert.That(floored.SelfSignatureBoost, Is.EqualTo(1.0), "boost clamps up to 1 (never quieter than normal)");

            var clone = (JammerAtb)j.Clone();
            Assert.That(clone.SensitivityDegrade, Is.EqualTo(4.0));
            Assert.That(clone.Range_m, Is.EqualTo(1e9));
            Assert.That(clone.SelfSignatureBoost, Is.EqualTo(5.0));
        }

        [Test]
        [Description("The spatial helper: a hostile jammer IN range degrades a receiver (>1); it never blinds its OWN faction; it's inert beyond its range; and an empty field is 1.0. Uses the real base-mod Havoc so the JSON → JammerAtb binding is exercised too.")]
        public void JammingDivisor_CountsHostileInRangeJammers_Only()
        {
            var s = TestScenario.CreateWithColony();
            var enemyFaction = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);
            var havoc = BuildShip(s, enemyFaction, Havoc, "Havoc");
            var havocPos = havoc.GetDataBlob<PositionDB>().AbsolutePosition;
            var field = new List<Entity> { havoc };

            Assert.That(JammerAtb.JammingDivisorAgainst(havocPos, s.Faction.Id, field), Is.GreaterThan(1.0),
                "a hostile jammer in range degrades a receiver of another faction");
            Assert.That(JammerAtb.JammingDivisorAgainst(havocPos, enemyFaction.Id, field), Is.EqualTo(1.0),
                "a jammer never blinds its OWN faction");
            var farPos = havocPos + new Vector3(3e9, 0, 0);   // beyond the 1 Gm jamming range
            Assert.That(JammerAtb.JammingDivisorAgainst(farPos, s.Faction.Id, field), Is.EqualTo(1.0),
                "out of range → no jamming");
            Assert.That(JammerAtb.JammingDivisorAgainst(havocPos, s.Faction.Id, new List<Entity>()), Is.EqualTo(1.0),
                "no jammers → 1.0");
        }

        [Test]
        [Description("The beacon catch: an active jammer lights its own ship up (SelfSignatureFactor > 1) so it's easy to find — but only when the flag is on; off → 1.0 (byte-identical). Cradle-to-grave: the base-mod Havoc builds from JSON with a live jammer.")]
        public void TheHavoc_BuildsFromJson_AndLightsItselfUp()
        {
            var s = TestScenario.CreateWithColony();
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns;
            Assert.That(designs.ContainsKey(Havoc), Is.True,
                "the Havoc loads onto the faction — the JSON jammer template + design + earth.json entries wired up (six-point registration)");

            var havoc = BuildShip(s, s.Faction, Havoc, "Havoc");
            Assert.That(havoc.GetDataBlob<ComponentInstancesDB>().TryGetComponentsByAttribute<JammerAtb>(out _), Is.True,
                "the Havoc carries a jammer (JSON jammer → JammerAtb wired)");

            bool saved = JammerAtb.EnableJamming;
            try
            {
                JammerAtb.EnableJamming = false;
                Assert.That(JammerAtb.SelfSignatureFactor(havoc), Is.EqualTo(1.0),
                    "flag off → no beacon → byte-identical");
                JammerAtb.EnableJamming = true;
                Assert.That(JammerAtb.SelfSignatureFactor(havoc), Is.GreaterThan(1.0),
                    "flag on → the active jammer runs its own signature UP (the beacon catch)");
            }
            finally { JammerAtb.EnableJamming = saved; }
        }

        [Test]
        [Description("End-to-end payoff: a watcher picks up a hostile bogey; drop a hostile jammer in range and the watcher's picture of the bogey SHRINKS (lower signal strength) — the barrage floods the band. With the flag off the same jammer is inert (byte-identical). Composes EW × Detection on the live scan.")]
        public void AHostileJammer_ShrinksTheWatchersPictureOfTheBogey()
        {
            var s = TestScenario.CreateWithColony();
            var enemyFaction = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);
            var watcher = BuildShip(s, s.Faction, "default-ship-design-test-capital", "Watcher");
            var bogey = BuildShip(s, enemyFaction, "default-ship-design-test-capital", "Bogey");

            bool saved = JammerAtb.EnableJamming;
            try
            {
                JammerAtb.EnableJamming = true;
                RunSensorScan(s);
                double baseline = SignalFor(watcher, bogey);
                Assert.That(baseline, Is.GreaterThan(0), "the watcher picks up the bogey with no jammer present");

                // Drop a hostile jammer near the watcher (same body → well within its 1 Gm range).
                var havoc = BuildShip(s, enemyFaction, Havoc, "Havoc");
                RunSensorScan(s);
                double jammed = SignalFor(watcher, bogey);
                Log($"bogey signal strength: baseline {baseline:0.###}, jammed {jammed:0.###}");
                Assert.That(jammed, Is.GreaterThan(0), "the bogey is still detected (point-blank), just weaker");
                Assert.That(jammed, Is.LessThan(baseline),
                    "a hostile jammer in range shrinks the watcher's picture of the bogey — the barrage floods the band");

                // Flag off → the jammer is inert → back to the baseline (byte-identical).
                JammerAtb.EnableJamming = false;
                RunSensorScan(s);
                Assert.That(SignalFor(watcher, bogey), Is.EqualTo(baseline).Within(1e-6),
                    "flag off → the jammer does nothing → detection is byte-identical");
            }
            finally { JammerAtb.EnableJamming = saved; }
        }
    }
}
