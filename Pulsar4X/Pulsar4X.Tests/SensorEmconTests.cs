using NUnit.Framework;
using Pulsar4X.Sensors;

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
    }
}
