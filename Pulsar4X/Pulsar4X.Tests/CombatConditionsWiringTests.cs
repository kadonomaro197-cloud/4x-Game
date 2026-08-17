using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pulsar4X.Combat;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Fleets;
using Pulsar4X.Galaxy;
using Pulsar4X.Hazards;
using Pulsar4X.Movement;
using Pulsar4X.Orbital;
using Pulsar4X.Ships;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// E-env slice 2b (OPERATION BLUEPRINT-TO-STEEL) — the LIVE wire that makes space combat environment-aware.
    /// Slice 1 built the <see cref="CombatConditions"/> reader; slice 2a added the accuracy hook to the shared kernel
    /// + the <see cref="FleetCombatStateDB.Conditions"/> storage. 2b (a) SEEDS a fleet's conditions from WHERE it
    /// fights at engagement start (<see cref="CombatEngagement.StartEngagement"/>) and (b) THREADS the defender's
    /// <c>Conditions.Accuracy</c> through the resolver's fire path (<c>ApplyCasualties → LandedFraction → kernel</c>),
    /// so a battle fought inside a nebula lands less fire. All behind <see cref="CombatEngagement.EnableCombatConditions"/>
    /// (default OFF → conditions stay Clean, accuracy 1.0 → byte-identical); these fixtures opt in and reset it in finally.
    /// </summary>
    [TestFixture]
    public class CombatConditionsWiringTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[cond-wire] " + m);

        private static Entity FirstStar(TestScenario s)
            => s.StartingSystem.GetAllDataBlobsOfType<StarInfoDB>().First().OwningEntity;

        private static Entity MakeFleet(TestScenario s, Entity faction, string name)
            => FleetFactory.Create(s.StartingSystem, faction.Id, name);

        /// <summary>A corvette under the fleet with a stamped, deterministic combat value (one always-hit beam).</summary>
        private static Entity AddShip(TestScenario s, Entity owner, Entity fleet, double evasion = 0,
            double firepower = 1e6, double toughness = 1e7)
        {
            var design = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns["default-ship-design-test-corvette"];
            var ship = ShipFactory.CreateShip(design, s.Faction, s.StartingBody, "ship");
            ship.FactionOwnerID = owner.Id;
            s.Game.OrderHandler.HandleOrder(FleetOrder.AssignShip(owner.Id, fleet, ship));

            var cv = new ShipCombatValueDB(firepower, toughness, 1.0);
            cv.Evasion = evasion;
            cv.Weapons = new List<WeaponProfile> { new WeaponProfile(firepower, 3e8, 1.0, 1.0, 0) };  // range 0 = unbounded
            ship.SetDataBlob(cv);
            return ship;
        }

        private static CombatConditions Conditions(Entity fleet)
            => fleet.GetDataBlob<FleetCombatStateDB>().Conditions;

        private static double Pool(Entity fleet)
            => fleet.TryGetDataBlob<FleetCombatStateDB>(out var st) ? st.DamageTakenPool : -1;

        [Test]
        [Description("SEED: with the flag ON, StartEngagement reads each fleet's battle conditions from WHERE it fights " +
                     "— inside a gas cloud, InHazard is true and Accuracy is cut below 1. With the flag OFF the conditions " +
                     "stay Clean (Accuracy 1.0), byte-identical.")]
        public void StartEngagement_InAHazard_SeedsCutConditions_FlagGated()
        {
            var s = TestScenario.CreateWithColony();
            var reds = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);

            var blue = MakeFleet(s, s.Faction, "Blue");
            var red = MakeFleet(s, reds, "Red");
            var blueShip = AddShip(s, s.Faction, blue);
            AddShip(s, reds, red);

            // Wrap the fleets' position in a gas cloud so they fight INSIDE a hazard.
            var star = FirstStar(s);
            var starPos = star.GetDataBlob<PositionDB>().AbsolutePosition;
            var shipPos = blueShip.GetDataBlob<PositionDB>().AbsolutePosition;
            SpaceHazardFactory.CreateGasCloud(s.StartingSystem, star, shipPos - starPos, Distance.AuToMt(0.5));

            // Flag OFF → Clean (byte-identical).
            CombatEngagement.StartEngagement(blue, red);
            Assert.That(Conditions(blue).InHazard, Is.False, "flag off: conditions stay Clean, no hazard read");
            Assert.That(Conditions(blue).Accuracy, Is.EqualTo(1.0).Within(1e-9));

            // Clear the state, flag ON → the hazard is read into Conditions.
            blue.RemoveDataBlob<FleetCombatStateDB>();
            red.RemoveDataBlob<FleetCombatStateDB>();
            CombatEngagement.EnableCombatConditions = true;
            try
            {
                CombatEngagement.StartEngagement(blue, red);
                Log($"in-cloud conditions: InHazard={Conditions(blue).InHazard} Accuracy={Conditions(blue).Accuracy:0.###}");
                Assert.That(Conditions(blue).InHazard, Is.True, "flag on + in a gas cloud: the fight is inside a hazard");
                Assert.That(Conditions(blue).Accuracy, Is.LessThan(1.0), "the cloud cuts accuracy (a nebula is cover)");
                Assert.That(Conditions(red).InHazard, Is.True, "both fleets read the hazard they share");
            }
            finally { CombatEngagement.EnableCombatConditions = false; }
        }

        [Test]
        [Description("THREAD: with the flag ON, a defender whose Conditions.Accuracy is cut takes LESS fire in a salvo " +
                     "than the same defender in Clean conditions (the accuracy coefficient reaches the resolver's landed " +
                     "fraction). With the flag OFF the cut Conditions are ignored → byte-identical.")]
        public void ApplyCasualties_ReadsConditionsAccuracy_LessFireLands_FlagGated()
        {
            double PoolAfterSalvo(bool flagOn, double accuracy)
            {
                var s = TestScenario.CreateWithColony();
                var reds = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);
                var attacker = MakeFleet(s, s.Faction, "Attacker");
                var defender = MakeFleet(s, reds, "Defender");
                AddShip(s, s.Faction, attacker, evasion: 0, firepower: 1e6);
                AddShip(s, reds, defender, evasion: 0, toughness: 1e9);   // fat toughness so it survives the salvo

                CombatEngagement.EnableCombatConditions = flagOn;
                try
                {
                    CombatEngagement.StartEngagement(attacker, defender);   // clean space → Clean seed
                    // Override the DEFENDER's conditions to a jammed (cut-accuracy) environment.
                    defender.GetDataBlob<FleetCombatStateDB>().Conditions = CombatConditions.FromHazard(new HazardModifiers
                    {
                        InAnyHazard = true, SensorRangeMultiplier = accuracy, MoveSpeedMultiplier = 1.0,
                        WarpSpeedMultiplier = 1.0, DamagePerSecond = 0.0, BlindsSensors = false,
                    });
                    CombatEngagement.StepEngagement(attacker, defender, 5.0);
                    return Pool(defender);
                }
                finally { CombatEngagement.EnableCombatConditions = false; }
            }

            double clean = PoolAfterSalvo(flagOn: true, accuracy: 1.0);   // Accuracy 1.0 = full fire
            double jammed = PoolAfterSalvo(flagOn: true, accuracy: 0.3);  // Accuracy 0.3 = a nebula cut
            double flagOff = PoolAfterSalvo(flagOn: false, accuracy: 0.3); // cut ignored when flag off

            Log($"defender damage pool — clean(acc 1.0)={clean:E2}  jammed(acc 0.3)={jammed:E2}  flag-off={flagOff:E2}");
            Assert.That(clean, Is.GreaterThan(0), "sanity: the defender takes fire in clean conditions");
            Assert.That(jammed, Is.LessThan(clean),
                "flag on: a cut-accuracy defender takes LESS fire (poor visibility lands fewer shots) — the 2b thread");
            Assert.That(flagOff, Is.EqualTo(clean).Within(clean * 1e-9),
                "flag off: the cut Conditions are ignored → byte-identical to the full-fire (Clean) salvo");
        }
    }
}
