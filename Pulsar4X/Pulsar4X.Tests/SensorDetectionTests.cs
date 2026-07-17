using System.Linq;
using NUnit.Framework;
using Pulsar4X.Components;
using Pulsar4X.Datablobs;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Movement;
using Pulsar4X.Sensors;
using Pulsar4X.Ships;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Detection gauge — the FIRST test on the sensor/contact layer (it was 🔴 DARK: built and rigorous, but
    /// never gauged). The M1 "detection" lever rides this existing engine, so before wiring fog-of-war into
    /// combat we prove the foundation here: a faction's ship actually DETECTS a hostile ship and a
    /// <see cref="Pulsar4X.Sensors"/> contact lands in that faction's per-system contact list
    /// (<c>StarSystem.GetSensorContacts(factionId)</c>).
    ///
    /// Why we fire the scan by hand: <c>SensorScan</c> is an <c>IInstanceProcessor</c> that is only scheduled by
    /// <c>Game.PostNewGameInitialization()</c> (the live New-Game path), which the colony test harness does NOT
    /// call — so nothing fires it in a bare test. (That gap is exactly why the layer shipped DARK.) We reproduce
    /// the live kick by invoking the processor on each sensor-bearing entity, the same call
    /// <c>PostNewGameInitialization</c> makes; reachable from the test assembly via
    /// <c>InternalsVisibleTo("Pulsar4X.Tests")</c>.
    /// </summary>
    [TestFixture]
    public class SensorDetectionTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[sensor-detection] " + m);

        /// <summary>Build a ship from the default capital design (it carries a passive sensor receiver AND a
        /// reactor that emits a signature, so it can both see and be seen) under the player faction, then stamp
        /// its true owner — the same build-then-flip idiom the combat fixtures use.</summary>
        private static Entity BuildShip(TestScenario s, Entity owner, string name)
        {
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns;
            var design = designs.TryGetValue("default-ship-design-test-capital", out var capital)
                ? capital
                : designs.Values.First();
            var ship = ShipFactory.CreateShip(design, s.Faction, s.StartingBody, name);
            ship.FactionOwnerID = owner.Id;
            return ship;
        }

        /// <summary>Fire the sensor scan once on every sensor-bearing entity in the starting system — exactly what
        /// <c>Game.PostNewGameInitialization</c> does at New Game, reproduced here because the harness doesn't.</summary>
        private static void RunSensorScan(TestScenario s)
        {
            foreach (var entity in s.StartingSystem.GetAllEntitiesWithDataBlob<SensorAbilityDB>())
                s.Game.ProcessorManager.GetInstanceProcessor(nameof(SensorScan))
                    .ProcessEntity(entity, s.Game.TimePulse.GameGlobalDateTime);
        }

        [Test]
        [Description("A faction's ship detects a hostile ship at point-blank: after one sensor scan, the player faction holds a SensorContact for the enemy ship. First gauge on the (DARK) sensor layer — the foundation the detection lever rides.")]
        public void SensorScan_DetectsHostileShip_AtPointBlank()
        {
            var s = TestScenario.CreateWithColony();
            var enemyFaction = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);

            var watcher = BuildShip(s, s.Faction, "Watcher");   // player ship — carries the sensor receiver
            var bogey = BuildShip(s, enemyFaction, "Bogey");    // hostile ship — emits the signature to be seen

            // Preconditions: assert the kit is present so a failure pinpoints WHAT is missing (receiver vs.
            // signature) instead of a confusing bare "no contact".
            Assert.That(watcher.HasDataBlob<SensorAbilityDB>(), Is.True, "the detecting ship must carry a sensor receiver");
            Assert.That(bogey.HasDataBlob<SensorProfileDB>(), Is.True, "the target ship must emit a sensor signature");

            // Before any scan there can be no contact — nothing has run the detector yet (this is the DARK state).
            Assert.That(s.StartingSystem.GetSensorContacts(s.Faction.Id).SensorContactExists(bogey.Id), Is.False,
                "no contact should exist before a scan has run");

            RunSensorScan(s);

            var contacts = s.StartingSystem.GetSensorContacts(s.Faction.Id);
            bool detected = contacts.SensorContactExists(bogey.Id);
            Log($"after scan: player faction detects bogey #{bogey.Id} = {detected}; total contacts held = {contacts.GetAllContacts().Count}");
            Assert.That(detected, Is.True,
                "the player faction should hold a SensorContact for the hostile ship after a scan at point-blank range");
        }

        [Test]
        [Description("Grave rung (detection × damage): shoot a watcher's sensor receivers off and recalc its " +
                     "abilities — exactly what the damage system does when a component is destroyed — and the ship's " +
                     "receiver cache empties, so it stops detecting. Lose your sensors, you go blind. This is the " +
                     "cradle-to-grave loss rung that wires detection to the damage system.")]
        public void DestroyingSensor_BlindsTheShip_GraveRung()
        {
            var s = TestScenario.CreateWithColony();
            var enemyFaction = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);

            var watcher = BuildShip(s, s.Faction, "Watcher");   // carries the sensor receiver
            var bogey = BuildShip(s, enemyFaction, "Bogey");     // emits a signature to be seen

            // Precondition + "before": the watcher has working receivers and a scan registers contacts.
            var ability = watcher.GetDataBlob<SensorAbilityDB>();
            Assert.That(ability.InstanceStates.Count, Is.GreaterThan(0), "the watcher must start with working sensor receivers");
            RunSensorScan(s);
            Assert.That(ability.CurrentContacts.Count, Is.GreaterThan(0), "with sensors, a scan registers contacts");

            // Shoot the sensors off: remove every sensor-receiver component, then recalc abilities — the tail of the
            // damage path (DamageProcessor removes a destroyed component, then calls ReCalcProcessor.ReCalcAbilities).
            var comps = watcher.GetDataBlob<ComponentInstancesDB>();
            comps.TryGetComponentsByAttribute<SensorReceiverAtb>(out var receivers);
            foreach (var receiver in receivers.ToList())
                comps.RemoveComponentInstance(receiver);
            ReCalcProcessor.ReCalcAbilities(watcher);

            // The grave rung: the receiver cache is now empty — the ship has no working sensors.
            Log($"after losing sensors: receivers cached = {ability.InstanceStates.Count} (expect 0)");
            Assert.That(ability.InstanceStates.Count, Is.EqualTo(0),
                "destroying the sensor components must clear the receiver cache — otherwise the ship scans with a phantom sensor");

            // ...and a fresh scan now detects nothing (the scan loop iterates the now-empty receiver list).
            RunSensorScan(s);
            Assert.That(ability.CurrentContacts.Count, Is.EqualTo(0),
                "a blinded ship detects nothing on its next scan");
        }

        [Test]
        [Description("Fog of war: a detected contact's DRAWN position is a SCAN SNAPSHOT (DataFrom.Sensors), not the " +
                     "target's live position — so the map blip shows where the target was last scanned and does NOT glide " +
                     "with the live ship. Regression for the developer's 'watched a ship cross empty space' report: a " +
                     "contact must never read src=LIVE (Parent), which tracked the real-time position frame by frame.")]
        public void DetectedContact_BlipIsAScanSnapshot_NotLive()
        {
            var s = TestScenario.CreateWithColony();
            var enemyFaction = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);

            var watcher = BuildShip(s, s.Faction, "Watcher");   // carries the sensor receiver
            var bogey = BuildShip(s, enemyFaction, "Bogey");     // emits a signature to be seen

            RunSensorScan(s);

            var contacts = s.StartingSystem.GetSensorContacts(s.Faction.Id);
            Assert.That(contacts.SensorContactExists(bogey.Id), Is.True, "the watcher should detect the bogey at point-blank");
            var contact = contacts.GetSensorContact(bogey.Id);

            // THE FIX: the blip is a SNAPSHOT (Sensors mode = "LAGGED"), never LIVE (Parent). A LIVE blip reads the
            // target's real-time position every frame and glides across the map even out of reach — the reported bug.
            Log($"contact blip source = {contact.PositionSourceLabel}");
            Assert.That(contact.PositionSourceLabel, Is.Not.EqualTo("LIVE"),
                "a detected contact's blip must be a scan snapshot, not the target's live position (the glide bug)");
            Assert.That(contact.PositionSourceLabel, Is.EqualTo("LAGGED"),
                "a freshly-detected contact should be a FRESH snapshot (DataFrom.Sensors)");

            // The snapshot captured WHERE THE TARGET WAS at scan time (proves it stored a real position, not a stub).
            var snapAtScan = contact.Position.AbsolutePosition;
            var bogeyPos = bogey.GetDataBlob<PositionDB>().AbsolutePosition;
            Assert.That((snapAtScan - bogeyPos).Length(), Is.LessThan(1.0),
                "the snapshot should equal the target's position at the moment of the scan");
        }
    }
}
