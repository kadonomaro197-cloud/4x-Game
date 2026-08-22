using Pulsar4X.Engine;
using Pulsar4X.Hazards;

namespace Pulsar4X.GroundCombat
{
    /// <summary>
    /// D-PLANFN-A — storms dim ground SIGHT (OPERATION BLUEPRINT-TO-STEEL tail D-planfn-A). A planet already grows
    /// weather as region hazards: <see cref="PlanetEnvironmentFactory"/> emits Ash / Dust / Lightning storms as
    /// <see cref="RegionEnvironment"/> rows with <see cref="HazardEffectType.SensorJam"/> (the SAME effect a space gas
    /// cloud uses to blind ships). Terrain and temperature-attrition already read the planet's environment; SIGHT did
    /// not — the ground radar reveal ignored storms. This helper closes that: it reads the SensorJam multiplier for a
    /// region so a unit standing in a storm sees LESS far — the ground echo of the space <c>SensorRangeMultiplier</c>.
    ///
    /// <para><b>How it's used.</b> <see cref="GroundSensors"/> multiplies a unit's computed radar range (km) by
    /// <see cref="SightMultAt"/> at the unit's region before converting to a hex reach — in BOTH the readout accessor
    /// (<c>RadarReachHexes</c>) and the per-tick reveal (<c>RevealFromUnits</c>). So a storm shrinks how far a unit
    /// scouts.</para>
    ///
    /// <para><b>Byte-identical + safe.</b> <see cref="EnableStormSight"/> defaults OFF → <see cref="SightMultAt"/>
    /// returns exactly 1.0 → the <c>rangeKm *= 1.0</c> wire is a no-op → every existing ground/sensor test is unchanged.
    /// (Flipped ON by <c>NewGameMenu</c> for a menu game, both start paths — the same pattern the other ground flags
    /// use.) Pure/read-only and NEVER THROWS — the reveal runs in the ground hotloop (landmine L4), so a null body /
    /// off-grid region / missing environment blob is a quiet "no dimming" (1.0), never an exception.</para>
    ///
    /// <para><b>Honest scope.</b> This dims how far a unit REVEALS new ground (the radar-reach path). Because the
    /// per-faction region reveal is additive/latching, a storm SLOWS new scouting; it does not re-blind ground already
    /// scouted. Blinding units mid-fight (the combat targeting gate) would be a separate, riskier change to the resolver
    /// and is deliberately NOT done here. Design: docs/ground/PLANETARY-FUNCTIONAL-PLAN-2026-07-27.md (D-planfn).</para>
    /// </summary>
    public static class GroundStormSight
    {
        /// <summary>Master gate. Default OFF → <see cref="SightMultAt"/> is always 1.0 → byte-identical. Flipped ON by
        /// <c>NewGameMenu</c> for a menu game (both start paths).</summary>
        public static bool EnableStormSight = false;

        /// <summary>
        /// The sight MULTIPLIER (0..1) storms impose on a unit's sensor/radar reach at <paramref name="regionIndex"/>
        /// on <paramref name="body"/>. 1.0 = clear (flag off, no storm, or any error). A <see cref="HazardEffectType.SensorJam"/>
        /// hazard's <see cref="RegionEnvironment.Magnitude"/> IS the multiplier (per the shared hazard vocabulary:
        /// 0.4 = reduced to 40%, 0 = fully blind); overlapping storms compound (multiply), mirroring the space
        /// <c>SensorRangeMultiplier *= MultiplierFor(SensorJam)</c>. Never throws (L4).
        /// </summary>
        public static double SightMultAt(Entity body, int regionIndex)
        {
            if (!EnableStormSight) return 1.0;
            try
            {
                if (body == null || regionIndex < 0) return 1.0;
                if (!body.TryGetDataBlob<PlanetEnvironmentsDB>(out var env) || env == null) return 1.0;

                double mult = 1.0;
                foreach (var e in env.ForRegion(regionIndex))
                {
                    if (e == null || e.Effect != HazardEffectType.SensorJam) continue;
                    double f = e.Magnitude;
                    if (f >= 1.0) continue;        // 1.0 = no effect (documented); a >1 value never AMPLIFIES sight
                    if (f < 0.0) f = 0.0;          // 0 = fully blind (documented); clamp a bad negative to that
                    mult *= f;                     // storms compound, like the space SensorJam multiplier
                }
                return mult;
            }
            catch { return 1.0; }
        }
    }
}
