using System;
using System.Collections.Generic;
using NUnit.Framework;
using Pulsar4X.Colonies;
using Pulsar4X.Combat;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Sensors;
using Pulsar4X.Ships;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Phase A-1 gauge — the FIGHT-or-FLEE lever wired into the CONQUER strike. `CombatRisk` was built + tested but
    /// had ZERO callers; the STRIKE rung now consults it, so the AI commits its warfleet only when the odds meet its
    /// RISK appetite. Proves (a) a faction that DETECTS an overwhelming enemy over the target does NOT sail the strike
    /// (it falls through to keep building) — the flee half; (b) with the SAME detected enemy at PARITY, a BOLD faction
    /// (Risk 1, engages at parity) strikes while a CAUTIOUS one (Risk 0, demands 2×) does not — the Risk trait sets the
    /// bar. The undetected-enemy → still-strikes case (graceful fog / byte-identity) is `MilitaryCompositionTests`.
    /// Drives the resolver directly (no clock advance beyond the 1-day fleet-assemble the mass helper needs, with order
    /// emission OFF so nothing fights). NOTE: strength UNITS aren't reconciled yet (own = combat-value, enemy =
    /// detected signal-kW) — this gauges the LEVER; the calibration is the A-3 tuning pass, so the test sets the enemy
    /// signal RELATIVE to the read own-strength rather than to an absolute number.
    /// </summary>
    [TestFixture]
    public class ConquerStrikeRiskTests
    {
        private static ShipDesign FirstWarshipDesign(Entity faction)
        {
            foreach (var d in faction.GetDataBlob<FactionInfoDB>().ShipDesigns.Values)
                if (d.TryGetComponentsByAttribute<Pulsar4X.Weapons.GenericBeamWeaponAtb>(out _)
                    || d.TryGetComponentsByAttribute<Pulsar4X.Weapons.RailgunWeaponAtb>(out _))
                    return d;
            return null;
        }

        private static void MassAStrikeFleet(TestScenario s, int count)
        {
            var armed = FirstWarshipDesign(s.Faction);
            Assert.That(armed, Is.Not.Null, "the start faction has an armed design to mass");
            var designs = new List<ShipDesign>();
            for (int i = 0; i < count; i++) designs.Add(armed);
            CombatSandbox.SpawnMixedFleet(s.Game, s.StartingSystem, s.Faction, s.Faction, designs, s.StartingBody, "Strike Group");
            s.AdvanceTime(TimeSpan.FromDays(1));   // let the AssignShip orders resolve the ships into the fleet (emission OFF → nothing fights)
        }

        /// <summary>Give a rival a colony on a fresh body in the strike fleet's OWN system (a reachable same-system target,
        /// so every OTHER strike gate passes and CombatRisk is the only thing that can block). Returns the rival colony body.</summary>
        private static void GiveRivalAColony(TestScenario s, Entity rival)
        {
            var mgr = s.StartingSystem;
            var body = Entity.Create();
            mgr.AddEntity(body);
            var colony = Entity.Create();
            colony.FactionOwnerID = rival.Id;
            mgr.AddEntity(colony);
            colony.SetDataBlob(new ColonyInfoDB(new Dictionary<int, long> { { 1, 1_000_000 } }, body));
            rival.GetDataBlob<FactionInfoDB>().Colonies.Add(colony);
        }

        /// <summary>Fabricate a live (non-memory) sensor contact so the observer "sees" a rival-owned entity at a set
        /// signal strength — the fog-limited strength CombatRisk reads via ThreatAssessment.DetectedStrengthOf.</summary>
        private static void SeeContact(TestScenario s, Entity observer, Entity rival, double strength)
        {
            var redShip = Entity.Create();
            redShip.FactionOwnerID = rival.Id;
            s.StartingSystem.AddEntity(redShip);
            var info = new SensorInfoDB { LatestDetectionQuality = new SensorReturnValues { SignalStrength_kW = strength } };
            observer.GetDataBlob<FactionInfoDB>().SensorContacts[redShip.Id] =
                new SensorContact { ActualEntity = redShip, ActualEntityId = redShip.Id, SensorInfo = info };
        }

        private static void SetRisk(Entity faction, double risk)
        {
            var p = new PersonalityDB();
            p.SetTrait(PersonalityTrait.Risk, risk);
            faction.SetDataBlob(p);
        }

        private static (TestScenario s, Entity reds) WarWithAMassedFleet()
        {
            var s = TestScenario.CreateWithColony();
            var reds = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);
            GiveRivalAColony(s, reds);
            Diplomacy.DeclareWar(s.Faction, reds, CasusBelli.ConfrontRival, s.Game.TimePulse.GameGlobalDateTime);
            MassAStrikeFleet(s, MilitaryComposition.StrikeGroupMinWarships);
            return (s, reds);
        }

        [Test]
        [Description("A faction that DETECTS an overwhelming enemy over the target does NOT sail the strike — the flee half of the fight/flee lever.")]
        public void Strike_BlockedWhenADetectedEnemyIsOverwhelming()
        {
            var (s, reds) = WarWithAMassedFleet();
            double own = FactionRollup.MilitaryStrength(s.Faction);
            Assert.That(own, Is.GreaterThan(0), "precondition: the attacker massed a real fleet");
            SeeContact(s, s.Faction, reds, own * 10.0);   // a detected enemy 10× our strength

            var action = new ConquerResolver().Resolve(FactionState.Snapshot(s.Faction),
                new StrategicObjectiveDB { Objective = StrategicObjective.Conquer });

            Assert.That(action.Kind, Is.Not.EqualTo("StrikeFleet"),
                "even a neutral-Risk faction won't sail its fleet into a detected overwhelming force — it keeps building");
        }

        [Test]
        [Description("At the SAME detected enemy strength (parity), a BOLD faction strikes while a CAUTIOUS one holds — the Risk trait sets the bar.")]
        public void Strike_RiskTraitSetsTheBar_BoldStrikesCautiousHolds()
        {
            // BOLD (Risk 1, engages at parity): own == enemy → strikes.
            var bold = WarWithAMassedFleet();
            double ownB = FactionRollup.MilitaryStrength(bold.s.Faction);
            SeeContact(bold.s, bold.s.Faction, bold.reds, ownB);   // enemy at PARITY
            SetRisk(bold.s.Faction, 1.0);
            var boldAction = new ConquerResolver().Resolve(FactionState.Snapshot(bold.s.Faction),
                new StrategicObjectiveDB { Objective = StrategicObjective.Conquer });
            Assert.That(boldAction.Kind, Is.EqualTo("StrikeFleet"),
                "a bold faction (Risk 1) commits at parity — own ≥ enemy × 1.0");

            // CAUTIOUS (Risk 0, demands 2×): own == enemy → holds.
            var cautious = WarWithAMassedFleet();
            double ownC = FactionRollup.MilitaryStrength(cautious.s.Faction);
            SeeContact(cautious.s, cautious.s.Faction, cautious.reds, ownC);   // same PARITY enemy
            SetRisk(cautious.s.Faction, 0.0);
            var cautiousAction = new ConquerResolver().Resolve(FactionState.Snapshot(cautious.s.Faction),
                new StrategicObjectiveDB { Objective = StrategicObjective.Conquer });
            Assert.That(cautiousAction.Kind, Is.Not.EqualTo("StrikeFleet"),
                "a cautious faction (Risk 0) refuses at parity — it demands own ≥ enemy × 2.0");
        }
    }
}
