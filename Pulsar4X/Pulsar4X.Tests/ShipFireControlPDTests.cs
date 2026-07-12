using NUnit.Framework;
using Pulsar4X.Combat;
using Pulsar4X.Datablobs;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Ships;
using Pulsar4X.Weapons;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Sensors ⚙3 — Fire Control ▸ PD-ONLY MODE (the `FinalFireOnly` dead-knob CONNECT, S3). A fire-control director
    /// carries three dials — `Range`, `TrackingSpeed`, `FinalFireOnly` — and the resolver historically read NONE of
    /// them (hit chance came off the weapon). S1 wired `TrackingSpeed`; this slice wires `FinalFireOnly`: a director
    /// flagged FinalFireOnly is a CIWS — it dedicates the ship's beams to INTERCEPTING incoming missiles (feeding the
    /// W6 point-defense pool) instead of anti-ship fire. Point the guns at the incoming salvo, not the enemy hull.
    ///
    /// Byte-identical for every current ship: the wire is gated behind <see cref="ShipCombatValueDB.EnableFinalFireOnlyPD"/>
    /// (default off, client-on), AND no base ship carries a FinalFireOnly director — so even turned ON nothing changes
    /// until one is installed. The new Sentinel CIWS Escort is a new example ship (no fixture perturbed). Engine-only → CI.
    /// </summary>
    [TestFixture]
    public class ShipFireControlPDTests
    {
        private const string Aegis = "default-ship-design-test-warship";   // laser warship, NORMAL director
        private const string Sentinel = "default-ship-design-test-sentinel"; // CIWS: lasers + a FinalFireOnly director
        private static void Log(string m) => TestContext.Progress.WriteLine("[fc-pd] " + m);

        [Test]
        [Description("The 3-arg binder ctor sets FinalFireOnly (non-zero = a PD director); the 2-arg ctor every existing template uses leaves it false; clone preserves it.")]
        public void BeamFireControlAtbDB_FinalFireOnly_FromTheThreeArgCtor()
        {
            var pd = new BeamFireControlAtbDB(10.0, 20000.0, 1.0);
            Assert.That(pd.FinalFireOnly, Is.True, "a non-zero third arg → FinalFireOnly (the PD/CIWS director)");
            var normal = new BeamFireControlAtbDB(100.0, 5000.0);
            Assert.That(normal.FinalFireOnly, Is.False, "the 2-arg ctor → not FinalFireOnly (every existing base-mod director)");
            Assert.That(((BeamFireControlAtbDB)pd.Clone()).FinalFireOnly, Is.True, "clone preserves FinalFireOnly");
        }

        [Test]
        [Description("Byte-identical: a ship whose director is a NORMAL (non-FinalFireOnly) one — the Aegis — is unaffected by the flag. With EnableFinalFireOnlyPD on OR off its firepower and point-defense are identical, because the PD-mode gate only bites a ship that actually carries a FinalFireOnly director.")]
        public void ANormalDirectorShip_IsUnaffectedByTheFlag()
        {
            var s = TestScenario.CreateWithColony();
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns;
            var aegis = ShipFactory.CreateShip(designs[Aegis], s.Faction, s.StartingBody, "Aegis");
            var instances = aegis.GetDataBlob<ComponentInstancesDB>();

            Assert.That(ShipCombatValueDB.HasLiveFinalFireOnlyDirector(instances), Is.False,
                "the Aegis's beam-fire-control is a normal director, not FinalFireOnly");

            bool saved = ShipCombatValueDB.EnableFinalFireOnlyPD;
            try
            {
                ShipCombatValueDB.EnableFinalFireOnlyPD = false;
                var off = ShipCombatValueDB.Calculate(aegis);
                ShipCombatValueDB.EnableFinalFireOnlyPD = true;
                var on = ShipCombatValueDB.Calculate(aegis);

                Assert.That(on.Firepower, Is.EqualTo(off.Firepower).Within(1e-6),
                    "no FinalFireOnly director → the flag is inert → firepower byte-identical");
                Assert.That(on.PointDefense_Jps, Is.EqualTo(off.PointDefense_Jps).Within(1e-6),
                    "and point-defense byte-identical too");
            }
            finally { ShipCombatValueDB.EnableFinalFireOnlyPD = saved; }
        }

        [Test]
        [Description("Cradle-to-grave: the base-mod Sentinel CIWS Escort builds from JSON with a FinalFireOnly director. With the flag ON its beams stop counting as anti-ship firepower and instead feed the point-defense pool (missile interception) — the whole beam damage/sec moves from Firepower to PointDefense_Jps. With the flag off it's an ordinary beam warship. Proves designed → built → the dead FinalFireOnly knob drives a real decision (a dedicated missile-interceptor).")]
        public void TheSentinel_TurnsItsBeamsIntoPointDefense_WhenTheFlagIsOn()
        {
            var s = TestScenario.CreateWithColony();
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns;
            Assert.That(designs.ContainsKey(Sentinel), Is.True,
                "the Sentinel loads onto the faction — the JSON pd-director template + design + earth.json entries wired up (six-point registration)");

            var sentinel = ShipFactory.CreateShip(designs[Sentinel], s.Faction, s.StartingBody, "Sentinel");
            var instances = sentinel.GetDataBlob<ComponentInstancesDB>();
            Assert.That(ShipCombatValueDB.HasLiveFinalFireOnlyDirector(instances), Is.True,
                "the Sentinel carries a live FinalFireOnly CIWS director (JSON pd-director → BeamFireControlAtbDB.FinalFireOnly wired)");

            bool saved = ShipCombatValueDB.EnableFinalFireOnlyPD;
            try
            {
                ShipCombatValueDB.EnableFinalFireOnlyPD = false;
                var off = ShipCombatValueDB.Calculate(sentinel);
                ShipCombatValueDB.EnableFinalFireOnlyPD = true;
                var on = ShipCombatValueDB.Calculate(sentinel);
                Log($"flag off: firepower={off.Firepower:0}, PD={off.PointDefense_Jps:0}  |  flag on: firepower={on.Firepower:0}, PD={on.PointDefense_Jps:0}");

                Assert.That(off.Firepower, Is.GreaterThan(0),
                    "flag off: the Sentinel's beams count as anti-ship firepower (an ordinary warship — byte-identical)");
                Assert.That(off.PointDefense_Jps, Is.EqualTo(0),
                    "flag off: no beam-sourced point-defense");
                Assert.That(on.PointDefense_Jps, Is.GreaterThan(0),
                    "flag on: the FinalFireOnly director routes the beams into the point-defense pool (missile interception)");
                Assert.That(on.Firepower, Is.LessThan(off.Firepower),
                    "flag on: those beams are no longer anti-ship firepower");
                Assert.That(on.PointDefense_Jps, Is.EqualTo(off.Firepower).Within(1e-6),
                    "the whole beam damage/sec moved wholesale from anti-ship firepower into point-defense — the CIWS trade");
            }
            finally { ShipCombatValueDB.EnableFinalFireOnlyPD = saved; }
        }
    }
}
