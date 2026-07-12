using NUnit.Framework;
using Pulsar4X.Energy;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Sensors;
using Pulsar4X.Ships;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Power ⚙4 ▸ signature/heat — REACTOR-LOAD HEAT (S5). A running power plant EMITS: the harder a ship's reactor
    /// works, the louder its heat signature, so a hot ship is seen from farther off (the dossier's ◐ wire, which the
    /// EmconActivityProcessor's own class comment flagged as "unblocked but not wired"). It folds the reactor's live
    /// <see cref="EnergyGenAbilityDB.Load"/> (0..1) into the same activity heat factor that thrust and firing already
    /// feed — so "run your reactor hard" joins "burn your drive / fire your guns" as things that light you up.
    ///
    /// Gated behind <see cref="EmconActivityProcessor.EnableReactorHeat"/> (default off → the term is never added →
    /// byte-identical, since a live reactor idles at some baseload; the client turns it on). Engine-only → CI.
    /// </summary>
    [TestFixture]
    public class ReactorHeatTests
    {
        private const string Aegis = "default-ship-design-test-warship"; // carries reactors → an EnergyGenAbilityDB
        private static void Log(string m) => TestContext.Progress.WriteLine("[reactor-heat] " + m);

        private static Entity BuildAegis(TestScenario s)
        {
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns;
            return ShipFactory.CreateShip(designs[Aegis], s.Faction, s.StartingBody, "Aegis");
        }

        [Test]
        [Description("ReactorLoad reads the ship's generator load straight off EnergyGenAbilityDB (0..1).")]
        public void ReactorLoad_ReadsTheGeneratorLoad()
        {
            var s = TestScenario.CreateWithColony();
            var ship = BuildAegis(s);
            var gen = ship.GetDataBlob<EnergyGenAbilityDB>();
            gen.Load = 0.7;
            Assert.That(EmconActivityProcessor.ReactorLoad(ship), Is.EqualTo(0.7),
                "ReactorLoad reflects the generator's current load");
        }

        [Test]
        [Description("The wire, cradle-to-grave payoff: with the flag on, running the reactor at full load raises the ship's emitted signature by exactly ReactorHeat over the same ship idle; with the flag off the reactor load is ignored (byte-identical). A hot plant lights you up.")]
        public void ReactorHeat_LoudensAShipUnderLoad_ByteIdenticalOff()
        {
            var s = TestScenario.CreateWithColony();
            var ship = BuildAegis(s);
            var gen = ship.GetDataBlob<EnergyGenAbilityDB>();
            var profile = ship.GetDataBlob<SensorProfileDB>();
            var proc = new EmconActivityProcessor();

            // Parked, not firing → the heat factor is 1.0, so the reactor term is the only variable.
            Assert.That(EmconActivityProcessor.IsBurning(ship), Is.False, "the parked Aegis is not burning");
            Assert.That(EmconActivityProcessor.IsFiring(ship), Is.False, "the parked Aegis is not firing");

            bool saved = EmconActivityProcessor.EnableReactorHeat;
            try
            {
                EmconActivityProcessor.EnableReactorHeat = true;

                gen.Load = 0.0;                         // reactor idle
                proc.ProcessEntity(ship, 5);
                double idle = profile.ActivityMultiplier;

                gen.Load = 1.0;                         // reactor at full load
                proc.ProcessEntity(ship, 5);
                double loaded = profile.ActivityMultiplier;
                Log($"activity multiplier: reactor idle={idle:0.000}, full load={loaded:0.000}");

                Assert.That(loaded, Is.GreaterThan(idle),
                    "a reactor under load makes the ship emit louder");
                Assert.That(loaded, Is.EqualTo(idle * (1.0 + EmconActivityProcessor.ReactorHeat)).Within(1e-9),
                    "full load adds exactly ReactorHeat to the heat factor");

                EmconActivityProcessor.EnableReactorHeat = false;
                gen.Load = 1.0;
                proc.ProcessEntity(ship, 5);
                Assert.That(profile.ActivityMultiplier, Is.EqualTo(idle).Within(1e-9),
                    "flag off → reactor load ignored → byte-identical");
            }
            finally { EmconActivityProcessor.EnableReactorHeat = saved; }
        }
    }
}
