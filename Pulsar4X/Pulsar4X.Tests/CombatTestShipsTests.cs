using System.Collections.Generic;
using NUnit.Framework;
using Pulsar4X.Combat;
using Pulsar4X.Energy;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Movement;
using Pulsar4X.Ships;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// The example armed ships for combat testing (combat spine step 10). Two purpose-built designs ship in the
    /// base mod (GameData/.../designs/shipDesigns.json) and are loaded onto the starting faction so they can be
    /// spawned from DevTools to set up a fight:
    ///   • <c>default-ship-design-test-warship</c> — "Aegis": 4 lasers, thick armour. The strong side.
    ///   • <c>default-ship-design-test-corvette</c> — "Picket": 1 laser, thin armour. The weak side.
    /// These tests are their sensor: they load as valid designs, rate strong-vs-weak, and produce a decisive
    /// auto-resolved fight. Engine-only -> runs in CI. (ShipCombatValueTests also auto-builds them with every
    /// faction design; this fixture pins the intended strong/weak RELATIONSHIP.)
    /// </summary>
    [TestFixture]
    public class CombatTestShipsTests
    {
        private const string Warship = "default-ship-design-test-warship";
        private const string Corvette = "default-ship-design-test-corvette";

        private static void Log(string m) => TestContext.Progress.WriteLine("[combat-test-ships] " + m);

        [Test]
        [Description("The Aegis warship and Picket corvette load onto the faction and rate strong-vs-weak — the warship out-guns and out-armours the corvette.")]
        public void TestShips_LoadAndRate_WarshipStrongerThanCorvette()
        {
            var s = TestScenario.CreateWithColony();
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns;

            Assert.That(designs.ContainsKey(Warship), Is.True, "the Aegis warship design should load onto the faction");
            Assert.That(designs.ContainsKey(Corvette), Is.True, "the Picket corvette design should load onto the faction");

            var warship = ShipFactory.CreateShip(designs[Warship], s.Faction, s.StartingBody, "Aegis");
            var corvette = ShipFactory.CreateShip(designs[Corvette], s.Faction, s.StartingBody, "Picket");

            var wv = warship.GetDataBlob<ShipCombatValueDB>();
            var cv = corvette.GetDataBlob<ShipCombatValueDB>();
            Log($"warship fp={wv.Firepower:0} tough={wv.Toughness:0}; corvette fp={cv.Firepower:0} tough={cv.Toughness:0}");

            Assert.That(wv.Firepower, Is.GreaterThan(0), "the warship carries weapons");
            Assert.That(cv.Firepower, Is.GreaterThan(0), "the corvette carries a weapon");
            Assert.That(wv.Firepower, Is.GreaterThan(cv.Firepower), "the warship's 4 lasers out-gun the corvette's 1");
            Assert.That(wv.Toughness, Is.GreaterThan(cv.Toughness), "the warship's thicker armour + more components make it tougher");
        }

        [Test]
        [Description("A fleet of Aegis warships decisively beats an equal-count fleet of Picket corvettes — the example ships give a clear, watchable test fight.")]
        public void WarshipFleet_Beats_CorvetteFleet()
        {
            var s = TestScenario.CreateWithColony();
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns;

            var warships = new List<Entity>();
            var corvettes = new List<Entity>();
            for (int i = 0; i < 3; i++)
            {
                warships.Add(ShipFactory.CreateShip(designs[Warship], s.Faction, s.StartingBody, "Aegis " + i));
                corvettes.Add(ShipFactory.CreateShip(designs[Corvette], s.Faction, s.StartingBody, "Picket " + i));
            }

            var result = AutoResolve.Resolve(warships, corvettes, new AutoResolveConfig());
            Log($"result={result.Outcome} rounds={result.RoundsElapsed}; warships lost {result.DestroyedA.Count}/3, corvettes lost {result.DestroyedB.Count}/3");

            Assert.That(result.Outcome, Is.EqualTo(BattleOutcome.SideAVictory), "the warship fleet should win");
            Assert.That(result.DestroyedB.Count, Is.EqualTo(3), "all corvettes destroyed");
            Assert.That(result.SurvivorsA, Is.GreaterThan(0), "warships survive");
        }

        [Test]
        [Description("A freshly built ship boots with an EMPTY reactor (0 stored energy) — too little to create a warp bubble, so a move order does nothing. ShipFactory.ChargeReactors tops it to capacity so it can warp immediately: the energy half of 'spawn ready to fly', the sibling of FillFuelTanks (the 'spawned ship won't move' fix).")]
        public void ChargeReactors_FillsStoredEnergy_SoASpawnedShipCanWarp()
        {
            var s = TestScenario.CreateWithColony();
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns;
            // Leviathan: reactor + 4 battery banks + an alcubierre-2k warp drive — a heavy combat hull.
            const string Capital = "default-ship-design-test-capital";
            Assert.That(designs.ContainsKey(Capital), Is.True, "the Leviathan design should load onto the faction");

            var ship = ShipFactory.CreateShip(designs[Capital], s.Faction, s.StartingBody, "Leviathan");
            var energy = ship.GetDataBlob<EnergyGenAbilityDB>();
            var warp = ship.GetDataBlob<WarpAbilityDB>();
            string eType = warp.EnergyType;

            double max = energy.EnergyStoreMax[eType];
            double storedBefore = energy.EnergyStored.TryGetValue(eType, out var b) ? b : 0;
            double bubbleCost = warp.BubbleCreationCost;
            Log($"Leviathan {eType}: storedBefore={storedBefore:0} max={max:0} bubbleCreationCost={bubbleCost:0}");

            // The bug: a brand-new ship can't warp because it boots with too little stored energy for the bubble.
            Assert.That(storedBefore, Is.LessThan(bubbleCost),
                "a freshly built ship starts with too little stored energy to create a warp bubble (the 'spawned ship won't move' cause)");

            double added = ShipFactory.ChargeReactors(ship);
            double storedAfter = energy.EnergyStored[eType];
            Log($"after ChargeReactors: stored={storedAfter:0} (+{added:0})");

            Assert.That(storedAfter, Is.EqualTo(max), "ChargeReactors tops the energy store off to its max capacity");
            Assert.That(storedAfter, Is.GreaterThanOrEqualTo(bubbleCost),
                "the charged ship now holds enough to create the warp bubble — it can warp on a move order");
            Assert.That(added, Is.GreaterThan(0), "energy was actually added (it started below capacity)");
        }
    }
}
