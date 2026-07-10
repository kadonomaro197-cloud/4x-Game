using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Movement;
using Pulsar4X.Ships;
using Pulsar4X.Storage;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Propulsion ⚙2 — the Exotic ▸ REACTIONLESS drive (no propellant, infinite delta-V). An ordinary rocket makes
    /// thrust by throwing reaction mass, so its thrust = exhaust velocity × fuel-burn rate and its delta-V is
    /// fuel-limited (Tsiolkovsky). A reactionless drive pushes without a reaction mass: it sets thrust DIRECTLY, burns
    /// no fuel, and its delta-V is effectively unlimited — so on the combat/closing resolver its fleet's maneuver
    /// reserve (`FleetCombat.DeltaVFloor` → `ManeuverBudget`) never depletes and it can kite forever.
    ///
    /// Cradle-to-grave in one slice: the <see cref="ReactionlessThrustAtb"/> component, the
    /// <see cref="NewtonThrustAbilityDB.Reactionless"/> flag (which pins delta-V unlimited and is guarded through the
    /// single fuel recompute funnel, <see cref="CargoTransferProcessor.UpdateMassFuelAndDeltaV"/> → SetFuel), and a
    /// buildable base-mod reactionless-drive on a NEW cruiser, the Nomad (no fuel-burning thruster at all). Byte-
    /// identical for every current ship (Reactionless defaults false → the ordinary fuel-limited Tsiolkovsky delta-V).
    ///
    /// v1 delivers the combat/closing payoff + the strategic delta-V readout; the deeper in-space burn model
    /// (NewtonianMovementProcessor executing a burn without consuming fuel) is a flagged follow-up. Engine-only → CI.
    /// </summary>
    [TestFixture]
    public class ShipReactionlessDriveTests
    {
        private const string Aegis = "default-ship-design-test-warship";   // NTR drive — fuel-limited, not reactionless
        private const string Nomad = "default-ship-design-test-nomad";     // reactionless-drive cruiser, no NTR
        private static void Log(string m) => TestContext.Progress.WriteLine("[reactionless] " + m);

        [Test]
        [Description("ReactionlessThrustAtb sets the Reactionless flag, adds thrust directly, and pins delta-V to ReactionlessDeltaV (unlimited); the flag survives a NewtonThrustAbilityDB clone.")]
        public void ReactionlessThrustAtb_SetsUnlimitedDeltaV()
        {
            var db = new NewtonThrustAbilityDB("");
            Assert.That(db.Reactionless, Is.False, "a fresh thrust DB is a normal (fuel-limited) rocket by default");
            Assert.That(new ReactionlessThrustAtb(200_000, 1_000_000).ThrustInNewtons, Is.EqualTo(200_000),
                "the reactionless drive carries its direct thrust (no exhaust-velocity × burn-rate)");

            db.Reactionless = true;
            db.SetFuel(0, 100_000); // the fuel path must NOT reset a reactionless drive's delta-V to a Tsiolkovsky value
            Assert.That(db.DeltaV, Is.EqualTo(NewtonThrustAbilityDB.ReactionlessDeltaV),
                "a reactionless drive's delta-V is pinned unlimited, not recomputed from fuel mass");
            Assert.That(((NewtonThrustAbilityDB)db.Clone()).Reactionless, Is.True, "clone preserves the reactionless flag");
        }

        [Test]
        [Description("Byte-identical: a real base-mod ship with an ORDINARY (NTR) drive reads Reactionless == false and a finite, fuel-limited delta-V — the fuel machinery is untouched (every current ship).")]
        public void ARealShip_WithAnOrdinaryDrive_IsFuelLimited()
        {
            var s = TestScenario.CreateWithColony();
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns;
            var ship = ShipFactory.CreateShip(designs[Aegis], s.Faction, s.StartingBody, "Aegis");
            var newt = ship.GetDataBlob<NewtonThrustAbilityDB>();

            Assert.That(newt.Reactionless, Is.False, "an NTR drive is a normal fuel-limited rocket → byte-identical");
            Assert.That(newt.DeltaV, Is.LessThan(NewtonThrustAbilityDB.ReactionlessDeltaV),
                "its delta-V is the ordinary finite fuel-limited value, nowhere near the reactionless sentinel");
            Log($"Aegis (NTR): reactionless={newt.Reactionless}, deltaV={newt.DeltaV:0} m/s");
        }

        [Test]
        [Description("Cradle-to-grave: the base-mod Nomad Reactionless Cruiser builds from JSON — its reactionless drive gives it thrust with NO fuel-burning engine, the Reactionless flag set, and an UNLIMITED delta-V that SURVIVES a fuel/mass recompute (the SetFuel/UpdateMassFuelAndDeltaV guard holds). So a player-built reactionless ship never runs dry and its maneuver reserve never depletes — designed → built → installed → kites forever. Proves the JSON reactionless-drive → ReactionlessThrustAtb → NewtonThrustAbilityDB chain.")]
        public void TheNomad_HasUnlimitedDeltaV_ThatSurvivesAFuelUpdate()
        {
            var s = TestScenario.CreateWithColony();
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns;
            Assert.That(designs.ContainsKey(Nomad), Is.True,
                "the Nomad loads onto the faction — the JSON reactionless-drive template + design + earth.json entries wired up (six-point registration)");

            var nomad = ShipFactory.CreateShip(designs[Nomad], s.Faction, s.StartingBody, "Nomad");
            var newt = nomad.GetDataBlob<NewtonThrustAbilityDB>();
            Log($"Nomad: reactionless={newt.Reactionless}, thrust={newt.ThrustInNewtons:0} N, deltaV={newt.DeltaV:e2} m/s");

            Assert.That(newt.Reactionless, Is.True,
                "the reactionless drive set the flag (JSON reactionless-drive → ReactionlessThrustAtb → NewtonThrustAbilityDB is wired)");
            Assert.That(newt.ThrustInNewtons, Is.GreaterThan(0), "the reactionless drive produces real thrust (no propellant)");
            Assert.That(newt.DeltaV, Is.EqualTo(NewtonThrustAbilityDB.ReactionlessDeltaV), "unlimited delta-V — never fuel-limited");

            // The guard holds through the fuel/cargo recompute funnel every cargo change runs (it must NOT reset the
            // reactionless delta-V to a Tsiolkovsky value).
            CargoTransferProcessor.UpdateMassFuelAndDeltaV(nomad);
            Assert.That(newt.DeltaV, Is.EqualTo(NewtonThrustAbilityDB.ReactionlessDeltaV),
                "a fuel/mass update leaves the reactionless delta-V unlimited (the SetFuel/UpdateMassFuelAndDeltaV guard works)");
        }
    }
}
