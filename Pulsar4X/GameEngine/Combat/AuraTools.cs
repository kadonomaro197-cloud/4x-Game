using System.Collections.Generic;
using Pulsar4X.Orbital;

namespace Pulsar4X.Combat
{
    /// <summary>
    /// The PURE aura math (E14 auras, Phase A) — a radius test + a magnitude falloff, with no <c>Entity</c>, no cargo,
    /// no processor — so it's unit-testable on its own even though the per-tick neighbour SWEEP that will call it
    /// (Phase B) rides live game state.
    ///
    /// <para>An "aura" is a field a component projects onto nearby units: a commander's rally, a synapse/psionic ward,
    /// a jamming bubble. Phase B walks the units near each projector and applies the buff, mirroring the hazard
    /// region-effect pattern (<see cref="Pulsar4X.Hazards.SpaceHazardTools"/> — "a region of space that affects ships
    /// inside it"). This class is just the geometry + magnitude those helpers hand each unit; the two guard-rails the
    /// SWEEP must enforce (take-the-BEST of overlapping auras, never SUM — <see cref="BestOf"/>; and a destroyed
    /// projector drops its field that tick, a health-scaled read = the grave rung) are noted here but applied there.</para>
    /// </summary>
    public static class AuraTools
    {
        /// <summary>Is <paramref name="target"/> within <paramref name="radius_m"/> of the aura's <paramref name="centre"/>?
        /// Pure. A non-positive radius reaches nothing; a unit exactly AT the edge is in range.</summary>
        public static bool InRange(Vector3 centre, Vector3 target, double radius_m)
            => radius_m > 0 && (target - centre).Length() <= radius_m;

        /// <summary>The aura's effective magnitude at distance <paramref name="dist_m"/> from its centre — FULL at the
        /// centre, tapering LINEARLY to 0 at the radius edge, and 0 beyond (so a unit at the fringe barely feels it and
        /// one outside not at all). Pure. A non-positive radius, a negative distance, or a distance at/beyond the edge
        /// all read 0.</summary>
        public static double MagnitudeAt(double baseMagnitude, double dist_m, double radius_m)
        {
            if (radius_m <= 0 || dist_m < 0 || dist_m >= radius_m) return 0.0;
            return baseMagnitude * (1.0 - dist_m / radius_m);
        }

        /// <summary>The "take the BEST, never SUM" guard-rail for overlapping auras of the same effect: the strongest
        /// single field wins, so two rally beacons don't stack (mirrors the shield/deflector "best installed" rule).
        /// Pure; an empty or null set reads 0.</summary>
        public static double BestOf(IEnumerable<double> magnitudes)
        {
            double best = 0.0;
            if (magnitudes == null) return best;
            foreach (var m in magnitudes) if (m > best) best = m;
            return best;
        }
    }
}
