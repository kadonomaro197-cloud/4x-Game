using System;
using System.Linq;
using NUnit.Framework;
using Pulsar4X.Components;
using Pulsar4X.Datablobs;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Movement;
using Pulsar4X.Orbital;
using Pulsar4X.Sensors;
using Pulsar4X.Ships;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// OPERATION EARTHFALL C2.1 — the fog-of-war contact-aging gauges the sensor layer never had (the GAPS the A2
    /// dossier named: "no test for age-out firing / Sensors→Memory flip"). The contact model was fixed 2026-07-17 so a
    /// detected blip is a SCAN SNAPSHOT that (a) flips to "last-known" (Memory) when its track is lost and (b) ages out
    /// and is FORGOTTEN once no sensor has re-detected it for <see cref="SensorScan.ContactStaleSeconds"/> — but nothing
    /// drove a target OUT OF RANGE to prove either edge actually fires. These do.
    ///
    /// Method (mirrors <see cref="SensorDetectionTests"/>): build a watcher (player) + a bogey (enemy) at point-blank,
    /// fire the scan by hand (the colony harness never schedules <see cref="SensorScan"/> — <c>PostNewGameInitialization</c>
    /// does, and the harness skips it), then MOVE the bogey far past every friendly sensor's reach (well beyond the
    /// homeworld's 200 Gm horizon) and fire the scan again at increasing game-times to watch the track flip, then age out.
    /// Reaches the internal <c>ProcessEntity(entity, atDateTime)</c> + <c>ContactStaleSeconds</c> via
    /// <c>InternalsVisibleTo("Pulsar4X.Tests")</c>.
    /// </summary>
    [TestFixture]
    public class EfClientSensorAgeOutTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[sensor-ageout] " + m);

        /// <summary>Build a ship from the default capital design (carries a passive sensor receiver AND a reactor that
        /// emits a signature, so it can both see and be seen), then stamp its true owner — the build-then-flip idiom the
        /// combat + detection fixtures use.</summary>
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

        /// <summary>Fire the sensor scan once on every sensor-bearing entity in the starting system AT AN EXPLICIT
        /// game-time — the same call <c>PostNewGameInitialization</c> makes, but with a chosen datetime so we can advance
        /// the "clock" the contact-aging math reads (it ages a track off <c>atDateTime - LastDetection</c>).</summary>
        private static void RunSensorScanAt(TestScenario s, DateTime at)
        {
            foreach (var entity in s.StartingSystem.GetAllEntitiesWithDataBlob<SensorAbilityDB>())
                s.Game.ProcessorManager.GetInstanceProcessor(nameof(SensorScan)).ProcessEntity(entity, at);
        }

        /// <summary>Move a ship far past every friendly sensor's reach — absolute, so it's independent of the ship's
        /// orbital parent (5e11 m = 500 Gm from the homeworld, well beyond the colony's 200 Gm hard horizon and any
        /// ship's ~0.3 Gm reach). Setting AbsolutePosition is an internal setter reachable via InternalsVisibleTo.</summary>
        private static void MoveFarOutOfRange(TestScenario s, Entity ship)
        {
            var homeAbs = s.StartingBody.GetDataBlob<PositionDB>().AbsolutePosition;
            ship.GetDataBlob<PositionDB>().AbsolutePosition = homeAbs + new Vector3(5e11, 0, 0);
        }

        [Test]
        [Description("Sensors→Memory lost-track flip: a detected contact whose track is LOST this scan (the target " +
                     "moved out of every sensor's reach) flips from a FRESH scan snapshot (LAGGED) to a last-known " +
                     "'FROZEN' fix — so the map fades it to '(last known)' — WITHOUT being removed yet (it's not stale). " +
                     "Drives SensorScan with a target moved out of range; gap the A2 dossier named as untested.")]
        public void TrackLost_FlipsToMemory_NotAgedOutYet()
        {
            var s = TestScenario.CreateWithColony();
            var enemyFaction = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);

            var watcher = BuildShip(s, s.Faction, "Watcher");   // carries the sensor receiver
            var bogey = BuildShip(s, enemyFaction, "Bogey");     // emits a signature to be seen

            var t0 = s.Game.TimePulse.GameGlobalDateTime;
            RunSensorScanAt(s, t0);   // detect at point-blank

            var contacts = s.StartingSystem.GetSensorContacts(s.Faction.Id);
            Assert.That(contacts.SensorContactExists(bogey.Id), Is.True, "the watcher should detect the bogey at point-blank");
            Assert.That(contacts.GetSensorContact(bogey.Id).PositionSourceLabel, Is.EqualTo("LAGGED"),
                "a freshly-detected contact is a FRESH scan snapshot (LAGGED)");

            // Track loss: move the bogey out of every friendly sensor's reach, then scan JUST after the first detect —
            // well under ContactStaleSeconds, so the contact must flip to last-known but NOT be forgotten yet.
            MoveFarOutOfRange(s, bogey);
            RunSensorScanAt(s, t0 + TimeSpan.FromHours(1));   // 3600 s << ContactStaleSeconds (14400 s)

            Assert.That(contacts.SensorContactExists(bogey.Id), Is.True,
                "the lost track must still be HELD (not yet stale) — you don't instantly forget a contact");
            var lost = contacts.GetSensorContact(bogey.Id);
            Log($"after track loss: source = {lost.PositionSourceLabel}, isMemory = {lost.PositionIsMemory}");
            Assert.That(lost.PositionSourceLabel, Is.EqualTo("FROZEN"),
                "a lost track must flip to a last-known 'FROZEN' fix (the map fades it to '(last known)')");
            Assert.That(lost.PositionIsMemory, Is.True, "PositionIsMemory should read true once the track is coasting on last-known");
        }

        [Test]
        [Description("ContactStaleSeconds age-out: a lost track that NO sensor re-detects for ContactStaleSeconds is " +
                     "REMOVED — the blip disappears, so a contact that left reach is FORGOTTEN instead of shown forever. " +
                     "Proves the THRESHOLD governs: still held just under it, gone just over it. The persistence half of " +
                     "the 'ship tracked across empty space forever' bug.")]
        public void StaleContact_AgesOut_AfterContactStaleSeconds()
        {
            var s = TestScenario.CreateWithColony();
            var enemyFaction = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);

            var watcher = BuildShip(s, s.Faction, "Watcher");
            var bogey = BuildShip(s, enemyFaction, "Bogey");

            var t0 = s.Game.TimePulse.GameGlobalDateTime;
            RunSensorScanAt(s, t0);   // detect at point-blank
            var contacts = s.StartingSystem.GetSensorContacts(s.Faction.Id);
            Assert.That(contacts.SensorContactExists(bogey.Id), Is.True, "the watcher should detect the bogey at point-blank");

            MoveFarOutOfRange(s, bogey);

            double staleSec = SensorScan.ContactStaleSeconds;

            // A scan UNDER the stale threshold: the track is lost and coasting, but must still be held (proving the
            // age-out is governed by the elapsed-since-last-detection threshold, not "any lost scan drops it").
            RunSensorScanAt(s, t0 + TimeSpan.FromSeconds(staleSec * 0.5));
            Assert.That(contacts.SensorContactExists(bogey.Id), Is.True,
                $"a lost track is still HELD before {staleSec} s have elapsed since its last detection");

            // A scan PAST the stale threshold: no sensor has re-detected it for > ContactStaleSeconds → forgotten.
            RunSensorScanAt(s, t0 + TimeSpan.FromSeconds(staleSec + 3600));
            Log($"contact held after {staleSec + 3600} s with no re-detection = {contacts.SensorContactExists(bogey.Id)} (expect false)");
            Assert.That(contacts.SensorContactExists(bogey.Id), Is.False,
                "a track no sensor has re-detected for ContactStaleSeconds must age out and be removed");
        }
    }
}
