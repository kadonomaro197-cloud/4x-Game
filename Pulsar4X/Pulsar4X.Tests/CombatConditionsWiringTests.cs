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

            var cv = new ShipCombatValueDB(firepower, toughness, 1.0) { Evasion = evasion };
            if (firepower > 0)   // firepower 0 => an UNARMED hull (a defender that only takes fire), like DodgeResolveTests.Hull
                cv.Weapons = new List<WeaponProfile> { new WeaponProfile(firepower, 3e8, 1.0, 1.0, 0) };  // beam: always-hit, range 0
            ship.SetDataBlob(cv);
            return ship;
        }

        private static CombatConditions Conditions(Entity fleet)
            => fleet.GetDataBlob<FleetCombatStateDB>().Conditions;

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
        [Description("THREAD: accuracy is a to-hit modifier (like evasion) — it raises effective toughness " +
                     "(Toughness ÷ landed), so it changes how many SALVOS kill the ship, not the raw damage pool. " +
                     "With the flag ON, a defender whose Conditions.Accuracy is cut takes MORE salvos to kill (poor " +
                     "visibility lands fewer shots per salvo) than the same defender in Clean conditions. With the flag " +
                     "OFF the cut Conditions are ignored → byte-identical. (Mirrors DodgeResolveTests' run-until-dead " +
                     "idiom — accuracy and evasion share the same landed-fraction mechanism.)")]
        public void ApplyCasualties_ReadsConditionsAccuracy_LessFireLands_FlagGated()
        {
            // Salvos to kill ONE unarmed defender hull under a steady always-hit beam. accuracy cuts landed → raises
            // effective toughness → more salvos. Calibration-free: only the RELATION (jammed > clean, flag-off == clean).
            int StepsToKill(bool flagOn, double accuracy)
            {
                var s = TestScenario.CreateWithColony();
                var reds = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);
                var attacker = MakeFleet(s, s.Faction, "Attacker");
                var defender = MakeFleet(s, reds, "Defender");
                AddShip(s, s.Faction, attacker, evasion: 0, firepower: 1e6);          // armed always-hit beam
                var defShip = AddShip(s, reds, defender, evasion: 0, firepower: 0, toughness: 1e6);  // UNARMED hull

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
                    int steps = 0;
                    while (defShip.IsValid && defender.HasDataBlob<FleetCombatStateDB>() && steps < 5000)
                    {
                        CombatEngagement.StepEngagement(attacker, defender, 5.0);
                        steps++;
                    }
                    return steps;
                }
                finally { CombatEngagement.EnableCombatConditions = false; }
            }

            int clean = StepsToKill(flagOn: true, accuracy: 1.0);    // Accuracy 1.0 = full fire → dies fast
            int jammed = StepsToKill(flagOn: true, accuracy: 0.3);   // Accuracy 0.3 = a nebula cut → dies slower
            int flagOff = StepsToKill(flagOn: false, accuracy: 0.3); // cut ignored when the flag is off

            Log($"salvos to kill the defender — clean(acc 1.0)={clean}  jammed(acc 0.3)={jammed}  flag-off={flagOff}");
            Assert.That(clean, Is.GreaterThan(0).And.LessThan(5000), "sanity: the defender dies under full fire within the cap");
            Assert.That(jammed, Is.GreaterThan(clean),
                "flag on: a cut-accuracy defender takes MORE salvos to kill (poor visibility lands fewer shots) — the 2b thread");
            Assert.That(flagOff, Is.EqualTo(clean),
                "flag off: the cut Conditions are ignored → byte-identical to the full-fire (Clean) result");
        }
    }
}
