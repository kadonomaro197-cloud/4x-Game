using NUnit.Framework;
using Pulsar4X.Combat;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Ships;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Propulsion ⚙2 — the Exotic ▸ INERTIALESS drive (the one genuinely-new resolver field the whole propulsion
    /// category needs). Normal evasion is bound to mass: a ship dodges only as well as its acceleration (thrust ÷ mass)
    /// lets it change vector, so a heavy capital is a sitting target. An inertialess drive breaks that coupling — it
    /// sets an evasion FLOOR decoupled from mass, so a dreadnought dodges like a corvette.
    ///
    /// This slice does it cradle-to-grave in one go: the <see cref="InertialessDriveAtb"/> component, the
    /// <see cref="ShipCombatValueDB.CalculateEvasion"/> read (a floor: max of the mass-bound evasion and the override,
    /// capped at <see cref="ShipCombatValueDB.EvasionCap"/>), and a buildable base-mod inertialess-drive on a NEW heavy
    /// capital, the Phantom. Byte-identical for every current ship (no drive → floor 0 → evasion is the ordinary
    /// mass-bound value); the new Phantom is a new example ship, so no combat fixture is perturbed. Engine-only → CI.
    /// </summary>
    [TestFixture]
    public class ShipInertialessDriveTests
    {
        private const string Aegis = "default-ship-design-test-warship";  // beam warship, no inertialess drive
        private const string Capital = "default-ship-design-test-capital"; // Leviathan — heavy, sluggish, no drive
        private const string Phantom = "default-ship-design-test-phantom"; // heavy capital WITH the inertialess drive
        private static void Log(string m) => TestContext.Progress.WriteLine("[inertialess] " + m);

        [Test]
        [Description("InertialessDriveAtb holds its evasion-override floor and clamps a negative to 0; clone preserves it.")]
        public void InertialessDriveAtb_PinsTheFoundation()
        {
            var d = new InertialessDriveAtb(0.8);
            Assert.That(d.EvasionOverride, Is.EqualTo(0.8), "the drive holds its evasion override");
            Assert.That(new InertialessDriveAtb(-0.1).EvasionOverride, Is.EqualTo(0), "a negative override clamps to 0");
            Assert.That(((InertialessDriveAtb)d.Clone()).EvasionOverride, Is.EqualTo(0.8), "clone preserves the override");
        }

        [Test]
        [Description("Byte-identical: a real base-mod ship with NO inertialess drive reads an evasion floor of 0, so its evasion is the ordinary mass-bound value — the resolve is untouched (every current ship, until a hull mounts the drive).")]
        public void ARealShip_WithNoInertialessDrive_ReadsZeroFloor()
        {
            var s = TestScenario.CreateWithColony();
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns;
            var ship = ShipFactory.CreateShip(designs[Aegis], s.Faction, s.StartingBody, "Aegis");

            Assert.That(ShipCombatValueDB.InertialessEvasionFloor(ship), Is.EqualTo(0),
                "no inertialess drive → 0 floor → evasion is the ordinary mass-bound value → byte-identical");
            var cv = ship.GetDataBlob<ShipCombatValueDB>();
            Log($"Aegis: evasion={cv.Evasion:0.00} (floor 0, mass-bound)");
        }

        [Test]
        [Description("Cradle-to-grave: the base-mod Phantom Inertialess Cruiser builds from JSON — it is a HEAVY capital hull (naturally near-zero evasion, like the Leviathan) but its inertialess drive lifts its evasion to the override floor, so it dodges like a fighter despite its mass. The Leviathan (an equally heavy capital with no drive) stays a sitting target — so the drive is the difference. Proves designed → built → installed → dodges, cradle-to-grave.")]
        public void ThePhantom_DodgesLikeAFighter_DespiteBeingAHeavyCapital()
        {
            var s = TestScenario.CreateWithColony();
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns;
            Assert.That(designs.ContainsKey(Phantom), Is.True,
                "the Phantom loads onto the faction — the JSON inertialess-drive template + design + earth.json entries wired up (six-point registration)");

            var phantom = ShipFactory.CreateShip(designs[Phantom], s.Faction, s.StartingBody, "Phantom");
            var leviathan = ShipFactory.CreateShip(designs[Capital], s.Faction, s.StartingBody, "Leviathan");
            double phantomEv = phantom.GetDataBlob<ShipCombatValueDB>().Evasion;
            double leviathanEv = leviathan.GetDataBlob<ShipCombatValueDB>().Evasion;
            Log($"evasion: Phantom (inertialess)={phantomEv:0.00}, Leviathan (heavy, no drive)={leviathanEv:0.00}");

            Assert.That(ShipCombatValueDB.InertialessEvasionFloor(phantom), Is.GreaterThan(0),
                "the Phantom's inertialess drive projects a real evasion floor (JSON inertialess-drive → InertialessDriveAtb → CalculateEvasion is wired)");
            Assert.That(phantomEv, Is.GreaterThan(0.7),
                "the Phantom dodges like a fighter — its inertialess drive floors evasion high despite its capital mass");
            Assert.That(phantomEv, Is.GreaterThan(leviathanEv + 0.3),
                "an equally heavy capital with no drive (the Leviathan) stays a sitting target — the inertialess drive is the difference");
        }
    }
}
