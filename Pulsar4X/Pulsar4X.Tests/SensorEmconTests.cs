using System.Linq;
using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Fleets;
using Pulsar4X.Sensors;
using Pulsar4X.Ships;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// EMCON gauge series — proves the detection-slice-3 wires that let a ship turn its own loudness up and down
    /// (EMCON = Emission Control: how much electromagnetic energy you choose to radiate). The lever the player
    /// will eventually pull is "run hot / cruise / go dark"; this fixture gauges each wire that lever rides on,
    /// one wire at a time, the same way the combat fixtures gauge one behavior per test.
    ///
    /// Slice 3a — the dynamic-signature foundation. Every detectable entity carries a
    /// <see cref="SensorProfileDB"/> with two kinds of EM output:
    ///   • EMITTED  — energy the ship makes itself (reactor heat, drive plume, active pings). You CAN turn this
    ///                down by running quiet. This is the dial EMCON controls.
    ///   • REFLECTED — energy that bounces off your hull when someone else lights you up with a radar. Going
    ///                quiet does NOT shrink your hull, so this is NOT under EMCON control.
    /// The wire under test is <see cref="SensorProfileDB.ActivityMultiplier"/>: a single runtime number
    /// (1.0 = as-designed) that the future EMCON processor sets from reactor load + thrust + weapons firing.
    /// <see cref="SensorTools.AttenuatedForDistance"/> multiplies the EMITTED spectra by it and leaves the
    /// REFLECTED spectra alone. Default 1.0 means detection is byte-for-byte unchanged until the lever moves it,
    /// which is why detection slices 1 and 2 stay green.
    ///
    /// Slice 3b — the player LEVER (<see cref="FleetEmcon"/>). 3a is the dial on one ship; 3b is the fleet-level
    /// Full/Cruise/Silent posture that pushes the right dial setting onto every ship in the fleet, and the gauge
    /// that the posture flows through the SAME attenuation math the sensor scan uses (posture → ship → detection).
    /// </summary>
    [TestFixture]
    public class SensorEmconTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[sensor-emcon] " + m);

        [Test]
        [Description("ActivityMultiplier is the EMCON dial: going dark (0.1) quiets the EMITTED band to one-tenth, " +
                     "but leaves the REFLECTED (radar-return) band untouched — running quiet doesn't shrink your hull. " +
                     "Default 1.0 is the no-change baseline that keeps detection slices 1 & 2 green.")]
        public void ActivityMultiplier_ScalesEmittedSignature_NotReflected()
        {
            // Two DISTINCT waveform objects so they land in separate dictionary keys. EMWaveForm keys a dictionary
            // by reference identity (no overridden Equals/GetHashCode — see EMWaveForm.cs), so a single object
            // reused in both lists would collapse into one summed key and we couldn't tell emitted from reflected.
            var emitWave = new EMWaveForm(400, 500, 600);      // the band the ship makes itself (controllable)
            var reflectWave = new EMWaveForm(1400, 1500, 1600); // the band bounced off the hull (not controllable)

            var profile = new SensorProfileDB();
            profile.EmittedEMSpectra.Add(new EMData { WaveForm = emitWave, Magnitude = 1000 });
            profile.ReflectedEMSpectra.Add(new EMData { WaveForm = reflectWave, Magnitude = 1000 });

            const double distance = 1000; // arbitrary; the same distance is used for every read so attenuation cancels out

            // Full power (the as-designed default the engine ships with).
            profile.ActivityMultiplier = 1.0;
            var full = SensorTools.AttenuatedForDistance(profile, distance);

            // Going dark: reactor idled, drive cold, weapons cold — one-tenth the emitted output.
            profile.ActivityMultiplier = 0.1;
            var dark = SensorTools.AttenuatedForDistance(profile, distance);

            Log($"emitted  band: full={full[emitWave]:E3} kW  dark={dark[emitWave]:E3} kW  (expect dark = 0.1x full)");
            Log($"reflected band: full={full[reflectWave]:E3} kW  dark={dark[reflectWave]:E3} kW  (expect unchanged)");

            // 1) Default 1.0 = no behavior change: with equal source magnitudes the emitted band reads the same as
            //    the reflected band, proving a multiplier of 1.0 doesn't touch the signal. This is the guard that
            //    keeps slices 1 & 2 (which never set the multiplier) byte-for-byte unchanged.
            Assert.That(full[emitWave], Is.EqualTo(full[reflectWave]).Within(1e-9).Percent,
                "at ActivityMultiplier 1.0 the emitted band must equal the equal-magnitude reflected band — the default must be a no-op");

            // 2) Going dark quiets the EMITTED band to exactly one-tenth of its full-power value.
            Assert.That(dark[emitWave], Is.EqualTo(full[emitWave] * 0.1).Within(1e-6).Percent,
                "going dark (0.1) must scale the emitted band to one-tenth — this is the EMCON wire");

            // 3) Going dark does NOT shrink the REFLECTED band. The radar return is identical at full power and
            //    dark, because quieting your reactor doesn't change your hull's cross-section.
            Assert.That(dark[reflectWave], Is.EqualTo(full[reflectWave]).Within(1e-9).Percent,
                "going dark must leave the reflected (radar-return) band untouched — EMCON doesn't shrink your hull");
        }

        [Test]
        [Description("The list-returning attenuation path (AttenuatedForDistanceList, used by the contact builder) " +
                     "honors the same EMCON dial: going dark scales the emitted entry and leaves the reflected entry alone.")]
        public void ActivityMultiplier_ScalesEmittedSignature_ListPath()
        {
            // Same setup as above but exercising the OTHER attenuation entry point. Both paths must obey the dial,
            // or a ship could go dark on one detection route and stay loud on the other.
            var emitWave = new EMWaveForm(400, 500, 600);
            var reflectWave = new EMWaveForm(1400, 1500, 1600);

            var profile = new SensorProfileDB();
            profile.EmittedEMSpectra.Add(new EMData { WaveForm = emitWave, Magnitude = 1000 });
            profile.ReflectedEMSpectra.Add(new EMData { WaveForm = reflectWave, Magnitude = 1000 });

            const double distance = 1000;

            // cullBelow: 0 — at this distance the attenuated magnitudes (~1e-5 kW) sit below the default 0.01 cull
            // floor; we disable the cull here so it can't masquerade as the wire under test. We're gauging the
            // multiplier, not the cull.
            profile.ActivityMultiplier = 1.0;
            var full = SensorTools.AttenuatedForDistanceList(profile, distance, cullBelow: 0);

            profile.ActivityMultiplier = 0.1;
            var dark = SensorTools.AttenuatedForDistanceList(profile, distance, cullBelow: 0);

            // Pull the magnitude for each band out of the returned list by matching the waveform object reference.
            double EmitMag(System.Collections.Generic.List<EMData> l) => Find(l, emitWave);
            double ReflectMag(System.Collections.Generic.List<EMData> l) => Find(l, reflectWave);

            Log($"list emitted : full={EmitMag(full):E3}  dark={EmitMag(dark):E3}");
            Log($"list reflected: full={ReflectMag(full):E3}  dark={ReflectMag(dark):E3}");

            Assert.That(EmitMag(dark), Is.EqualTo(EmitMag(full) * 0.1).Within(1e-6).Percent,
                "list path: going dark must scale the emitted entry to one-tenth");
            Assert.That(ReflectMag(dark), Is.EqualTo(ReflectMag(full)).Within(1e-9).Percent,
                "list path: going dark must leave the reflected entry untouched");
        }

        /// <summary>Find the magnitude of the entry whose waveform is the given object reference (EMWaveForm keys
        /// by reference identity), or 0 if the band was culled out. Asserts the band survived so a cull doesn't
        /// silently masquerade as a zero reading.</summary>
        private static double Find(System.Collections.Generic.List<EMData> list, EMWaveForm wave)
        {
            foreach (var d in list)
                if (ReferenceEquals(d.WaveForm, wave))
                    return d.Magnitude;
            Assert.Fail("expected band not present in attenuated list (was it culled below the floor?)");
            return 0;
        }

        // ---- Slice 3b — the EMCON posture LEVER (FleetEmcon.SetPosture) ---------------------------------------
        // 3a gave us the dial (ActivityMultiplier) on a ship. 3b is the player's hand on it: a fleet-level
        // Full/Cruise/Silent posture that pushes the right dial setting onto every ship in the fleet. These gauges
        // prove the full stack the lever rides: posture (fleet choice) -> each ship's ActivityMultiplier -> the
        // emitted signal in the same detection math the sensor scan uses.

        private static Entity MakeFleet(TestScenario s, Entity faction, string name)
            => FleetFactory.Create(s.StartingSystem, faction.Id, name);

        /// <summary>Nest a sub-fleet (fleet component) under a parent — the real ChangeParent order, same idiom as
        /// the combat fixtures. Lets us prove SetPosture recurses into components (the whole fleet goes dark).</summary>
        private static Entity MakeComponent(TestScenario s, Entity faction, Entity parentFleet, string name)
        {
            var component = FleetFactory.Create(s.StartingSystem, faction.Id, name);
            s.Game.OrderHandler.HandleOrder(FleetOrder.ChangeParent(faction.Id, component, parentFleet));
            return component;
        }

        /// <summary>Build a sensor-capable ship (the capital design carries a reactor signature, so it has a
        /// SensorProfileDB to dial) under the player faction, flip its owner, and assign it to the fleet.</summary>
        private static Entity AddSensingShip(TestScenario s, Entity faction, Entity fleet, string name)
        {
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns;
            var design = designs.TryGetValue("default-ship-design-test-capital", out var cap) ? cap : designs.Values.First();
            var ship = ShipFactory.CreateShip(design, s.Faction, s.StartingBody, name);
            ship.FactionOwnerID = faction.Id;
            s.Game.OrderHandler.HandleOrder(FleetOrder.AssignShip(faction.Id, fleet, ship));
            return ship;
        }

        private static double Mult(Entity ship) => ship.GetDataBlob<SensorProfileDB>().ActivityMultiplier;

        [Test]
        [Description("Setting a fleet's EMCON posture pushes the matching signature scale onto EVERY member ship's " +
                     "ActivityMultiplier — including ships in sub-fleet components — and the change is reversible. " +
                     "A ship in no posture-set fleet stays at the as-designed 1.0.")]
        public void SetPosture_PushesScaleToAllMemberShips_RecursingComponents()
        {
            var s = TestScenario.CreateWithColony();

            var fleet = MakeFleet(s, s.Faction, "Task Force");
            var shipA = AddSensingShip(s, s.Faction, fleet, "Direct A");
            var shipB = AddSensingShip(s, s.Faction, fleet, "Direct B");
            var component = MakeComponent(s, s.Faction, fleet, "Picket Component"); // a sub-fleet of the task force
            var shipC = AddSensingShip(s, s.Faction, component, "Component C");      // ship inside the sub-fleet

            // A loose ship in no fleet — the control: the lever must not touch ships outside the fleet.
            var looseFleet = MakeFleet(s, s.Faction, "Unrelated");
            var looseShip = AddSensingShip(s, s.Faction, looseFleet, "Loose");

            // Precondition: every ship starts at the as-designed default (1.0) — nothing has moved the dial.
            foreach (var ship in new[] { shipA, shipB, shipC, looseShip })
                Assert.That(Mult(ship), Is.EqualTo(1.0).Within(1e-12), "ships start at the as-designed full signature");
            Assert.That(FleetEmcon.PostureOf(fleet), Is.EqualTo(EmconPosture.Full), "a fleet with no order set reads Full");

            // Pull the lever: the whole task force goes dark.
            FleetEmcon.SetPosture(fleet, EmconPosture.Silent);

            Assert.That(FleetEmcon.PostureOf(fleet), Is.EqualTo(EmconPosture.Silent), "the fleet now holds the Silent posture");
            Assert.That(FleetEmcon.MultiplierOf(fleet), Is.EqualTo(FleetEmcon.SilentMultiplier).Within(1e-12));
            Log($"after Silent: A={Mult(shipA):F3} B={Mult(shipB):F3} C(component)={Mult(shipC):F3} loose={Mult(looseShip):F3}");

            // Every ship in the fleet — direct AND in the sub-component — is now quiet at the Silent scale.
            foreach (var ship in new[] { shipA, shipB, shipC })
                Assert.That(Mult(ship), Is.EqualTo(FleetEmcon.SilentMultiplier).Within(1e-12),
                    "every member ship (including in a sub-component) takes the fleet's Silent signature scale");

            // The loose ship in a different, un-posed fleet is untouched — the lever is scoped to its fleet.
            Assert.That(Mult(looseShip), Is.EqualTo(1.0).Within(1e-12),
                "a ship outside the posture-set fleet must keep its as-designed signature");

            // Reversible (the BonusesDB pattern, like doctrine): flip back to Full and the dial returns to 1.0.
            FleetEmcon.SetPosture(fleet, EmconPosture.Full);
            foreach (var ship in new[] { shipA, shipB, shipC })
                Assert.That(Mult(ship), Is.EqualTo(1.0).Within(1e-12), "going back to Full restores the as-designed signature");
        }

        [Test]
        [Description("The end-to-end stack: a fleet's posture flows all the way into the SAME attenuation math the " +
                     "sensor scan uses. A Silent fleet's ship emits one-tenth-ish (the Silent scale) the signal it " +
                     "does at Full, at the same range — so going dark really does shrink how far off you can be seen.")]
        public void FleetPosture_FlowsIntoTheRealDetectionMath()
        {
            var s = TestScenario.CreateWithColony();
            var fleet = MakeFleet(s, s.Faction, "Lone Scout");
            var ship = AddSensingShip(s, s.Faction, fleet, "Scout");
            var profile = ship.GetDataBlob<SensorProfileDB>();

            const double distance = 50_000; // arbitrary; same range used for both reads, so attenuation cancels

            // The ship genuinely emits something (kit present) and, fresh, has no radar-return set yet — so the
            // attenuated total is pure EMITTED signature, which is the part EMCON scales. (Reflection is set by the
            // hourly SensorReflectionProcessor, which the colony harness doesn't run.)
            Assert.That(profile.EmittedEMSpectra.Count, Is.GreaterThan(0), "a capital ship must emit a reactor signature to dial");
            Assert.That(profile.ReflectedEMSpectra.Count, Is.EqualTo(0),
                "fresh ship has no radar-return set yet, so the total is pure emitted — the band EMCON controls");

            FleetEmcon.SetPosture(fleet, EmconPosture.Full);
            double fullTotal = SensorTools.AttenuatedForDistance(profile, distance).Values.Sum();

            FleetEmcon.SetPosture(fleet, EmconPosture.Silent);
            double darkTotal = SensorTools.AttenuatedForDistance(profile, distance).Values.Sum();

            Log($"detection-math signal at {distance:N0} m: Full={fullTotal:E3}  Silent={darkTotal:E3}  ratio={darkTotal / fullTotal:F3}");

            Assert.That(fullTotal, Is.GreaterThan(0), "the ship must produce a detectable signal at Full");
            Assert.That(darkTotal, Is.EqualTo(fullTotal * FleetEmcon.SilentMultiplier).Within(1e-6).Percent,
                "going Silent must scale the detectable signal to the Silent multiplier — the posture reaches the real detection math");
        }

        // ---- Slice 3c — signature responds to ACTIVITY (EmconActivityProcessor) -------------------------------
        // Posture sets the baseline; this slice makes running hot betray you. The heat MATH is gauged here (pure,
        // CI-robust); the cross-system reads (a real burning/firing ship gets louder) are verified LIVE — standing
        // up a genuinely thrusting/firing ship in a unit test is fragile, and the processor auto-runs every 5 s, so
        // the developer's fly-a-ship / fire-the-guns test exercises the Movement↔Sensors / Weapons↔Sensors links.

        [Test]
        [Description("The heat factor (pure): cold/idle = 1.0; thrusting and firing each add their heat and stack. " +
                     "1.0 means no change, so an idle ship keeps its posture signature exactly.")]
        public void HeatFactor_RisesWithActivity_StacksThrustAndFiring()
        {
            Assert.That(EmconActivityProcessor.HeatFactor(false, false), Is.EqualTo(1.0).Within(1e-12),
                "cold and idle: heat factor is 1.0 (no change to the posture signature)");
            Assert.That(EmconActivityProcessor.HeatFactor(true, false),
                Is.EqualTo(1.0 + EmconActivityProcessor.ThrustHeat).Within(1e-12), "thrusting adds thrust heat");
            Assert.That(EmconActivityProcessor.HeatFactor(false, true),
                Is.EqualTo(1.0 + EmconActivityProcessor.WeaponHeat).Within(1e-12), "firing adds weapon heat");
            Assert.That(EmconActivityProcessor.HeatFactor(true, true),
                Is.EqualTo(1.0 + EmconActivityProcessor.ThrustHeat + EmconActivityProcessor.WeaponHeat).Within(1e-12),
                "thrust and firing heat stack");
            // Thrust is the dominant signal (the developer's 'full burn seen long before idle' picture).
            Assert.That(EmconActivityProcessor.ThrustHeat, Is.GreaterThan(EmconActivityProcessor.WeaponHeat),
                "a lit drive plume should be louder than a weapon discharge");
        }

        [Test]
        [Description("Final scale = posture base × activity heat. A burning Silent ship is far louder than an idle " +
                     "one — you can't burn quietly — yet a Silent posture still buys quiet vs the same activity at Full.")]
        public void ComputeActivityMultiplier_ComposesPostureAndActivity()
        {
            double silentIdle    = EmconActivityProcessor.ComputeActivityMultiplier(FleetEmcon.SilentMultiplier, false, false);
            double silentBurning = EmconActivityProcessor.ComputeActivityMultiplier(FleetEmcon.SilentMultiplier, true,  false);
            double fullIdle      = EmconActivityProcessor.ComputeActivityMultiplier(FleetEmcon.FullMultiplier,   false, false);
            double fullBurning   = EmconActivityProcessor.ComputeActivityMultiplier(FleetEmcon.FullMultiplier,   true,  false);

            // Idle composes to exactly the posture base (heat = 1).
            Assert.That(silentIdle, Is.EqualTo(FleetEmcon.SilentMultiplier).Within(1e-12));
            Assert.That(fullIdle, Is.EqualTo(FleetEmcon.FullMultiplier).Within(1e-12));
            // Burning betrays the hiding attempt: a Silent ship lights up hard when its drive is lit.
            Assert.That(silentBurning, Is.GreaterThan(silentIdle), "a burning Silent ship is much louder than an idle one — you can't burn quietly");
            // ...but the posture still helps: Silent+burning is quieter than Full+burning (same activity, lower base).
            Assert.That(silentBurning, Is.LessThan(fullBurning), "a Silent posture still buys quiet against the same activity at Full");
            Assert.That(silentBurning, Is.EqualTo(FleetEmcon.SilentMultiplier * (1.0 + EmconActivityProcessor.ThrustHeat)).Within(1e-12));
        }

        [Test]
        [Description("End-to-end through the real processor on real (idle, coasting) ships: after Silent posture, " +
                     "running the EmconActivityProcessor leaves each ship at exactly the Silent base — at idle the " +
                     "heat factor is 1, so the processor reads the posture base, composes, and writes ActivityMultiplier.")]
        public void Processor_OnIdleFleet_LeavesSignatureAtPostureBase()
        {
            var s = TestScenario.CreateWithColony();
            var fleet = MakeFleet(s, s.Faction, "Quiet Squadron");
            var shipA = AddSensingShip(s, s.Faction, fleet, "A");
            var shipB = AddSensingShip(s, s.Faction, fleet, "B");

            FleetEmcon.SetPosture(fleet, EmconPosture.Silent);

            // These ships sit at a body (OrbitDB, not NewtonMoveDB) and carry no fire-control firing kit, so they
            // are genuinely idle — the robust state to gauge the processor's composition on.
            Assert.That(EmconActivityProcessor.IsBurning(shipA), Is.False, "a ship parked at a body is not burning");
            Assert.That(EmconActivityProcessor.IsFiring(shipA), Is.False, "a ship with no fire-control kit is not firing");

            var proc = new EmconActivityProcessor();
            proc.ProcessEntity(shipA, 5);
            proc.ProcessEntity(shipB, 5);

            Log($"after processor on idle Silent fleet: A={Mult(shipA):F3} B={Mult(shipB):F3} (expect {FleetEmcon.SilentMultiplier})");
            foreach (var ship in new[] { shipA, shipB })
                Assert.That(Mult(ship), Is.EqualTo(FleetEmcon.SilentMultiplier).Within(1e-12),
                    "idle (heat 1) ⇒ the processor leaves the ship at its posture base — posture × activity composed correctly");
        }
    }
}
