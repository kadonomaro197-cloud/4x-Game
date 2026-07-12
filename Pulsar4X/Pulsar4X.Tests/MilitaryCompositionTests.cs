using System;
using System.Collections.Generic;
using NUnit.Framework;
using Pulsar4X.Colonies;
using Pulsar4X.Combat;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Ships;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// P-3 military-reach gauge, slice 2 (docs/AI-BRAIN-BUILD-TRACKER.md — "make the AI project force, not just build
    /// it"). Proves (a) <see cref="MilitaryComposition.ReadyStrikeFleet"/> recognises a MASSED strike group (≥ the
    /// threshold of armed hulls) — the developer's "move as a mass fleet"; and (b) the CONQUER resolver, given a
    /// scored enemy target (a war-declared rival's colony) AND a ready strike fleet, DECIDES to sail it — the "do"
    /// that composes 3.4b (declare war) → MilitaryTarget (name the world) → MilitaryComposition (mass) → the strike.
    /// The default faction (no war) stays on the "QueueWarship" build rung — the byte-identity tripwire in
    /// <see cref="ConquerResolverTests"/>.
    /// </summary>
    [TestFixture]
    public class MilitaryCompositionTests
    {
        /// <summary>First armed design the faction can build (mounts a beam or railgun) — the hull we mass.</summary>
        private static ShipDesign FirstWarshipDesign(Entity faction)
        {
            foreach (var d in faction.GetDataBlob<FactionInfoDB>().ShipDesigns.Values)
                if (d.TryGetComponentsByAttribute<Pulsar4X.Weapons.GenericBeamWeaponAtb>(out _)
                    || d.TryGetComponentsByAttribute<Pulsar4X.Weapons.RailgunWeaponAtb>(out _))
                    return d;
            return null;
        }

        /// <summary>Mass a player-owned fleet of <paramref name="count"/> armed hulls at the home body (built + fuelled
        /// + charged via the sandbox recipe), then advance a day so the AssignShip orders resolve into the fleet.</summary>
        private static Entity MassAStrikeFleet(TestScenario s, int count)
        {
            var armed = FirstWarshipDesign(s.Faction);
            Assert.That(armed, Is.Not.Null, "the start faction has an armed design to mass");
            var designs = new List<ShipDesign>();
            for (int i = 0; i < count; i++) designs.Add(armed);
            var fleet = CombatSandbox.SpawnMixedFleet(s.Game, s.StartingSystem, s.Faction, s.Faction, designs, s.StartingBody, "Strike Group");
            s.AdvanceTime(TimeSpan.FromDays(1));   // let the AssignShip orders resolve the ships into the fleet
            return fleet;
        }

        /// <summary>Give a rival a colony on a fresh body (so MilitaryTarget can name it as a strike target).</summary>
        private static void GiveRivalAColony(TestScenario s, Entity rival)
        {
            var mgr = s.Game.GlobalManager;
            var body = Entity.Create();
            mgr.AddEntity(body);
            var colony = Entity.Create();
            colony.FactionOwnerID = rival.Id;
            mgr.AddEntity(colony);
            colony.SetDataBlob(new ColonyInfoDB(new Dictionary<int, long> { { 1, 1_000_000 } }, body));
            rival.GetDataBlob<FactionInfoDB>().Colonies.Add(colony);
        }

        [Test]
        [Description("A fleet of 3 armed hulls reads as a READY strike group; the count reflects the armed hulls.")]
        public void ReadyStrikeFleet_RecognisesAMassedGroup()
        {
            var s = TestScenario.CreateWithColony();
            MassAStrikeFleet(s, MilitaryComposition.StrikeGroupMinWarships);

            var state = FactionState.Snapshot(s.Faction);
            var ready = MilitaryComposition.ReadyStrikeFleet(state);

            Assert.That(ready.IsValid, Is.True, "a fleet at/over the threshold of armed hulls is a ready strike group");
            Assert.That(MilitaryComposition.WarshipCount(ready), Is.GreaterThanOrEqualTo(MilitaryComposition.StrikeGroupMinWarships),
                "the ready fleet carries at least the strike threshold of armed hulls");
        }

        [Test]
        [Description("Conquer with a war-declared enemy colony AND a massed fleet DECIDES to sail it (StrikeFleet); pure decision.")]
        public void Conquer_WithATargetAndAMassedFleet_SailsTheStrike()
        {
            var s = TestScenario.CreateWithColony();
            var reds = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);
            GiveRivalAColony(s, reds);
            Diplomacy.DeclareWar(s.Faction, reds, CasusBelli.ConfrontRival, s.Game.TimePulse.GameGlobalDateTime);

            MassAStrikeFleet(s, MilitaryComposition.StrikeGroupMinWarships);

            var state = FactionState.Snapshot(s.Faction);
            var action = new ConquerResolver().Resolve(state, new StrategicObjectiveDB { Objective = StrategicObjective.Conquer });

            Assert.That(action.Kind, Is.EqualTo("StrikeFleet"),
                "with a scored enemy world and a mass fleet ready, Conquer sails the strike (not just builds more)");
        }
    }
}
