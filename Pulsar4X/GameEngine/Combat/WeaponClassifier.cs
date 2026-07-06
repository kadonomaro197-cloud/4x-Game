namespace Pulsar4X.Combat
{
    /// <summary>
    /// Computes a weapon's TRIANGLE CORNER (<see cref="WeaponClass"/>) from its DELIVERY axis + its dialled specs —
    /// the first concrete step of the weapon-designer UNIFICATION (docs/WEAPON-TAXONOMY-DESIGN.md, the developer's
    /// "one weapon designer, pick Nature × Delivery, the triangle EMERGES"). Today <see cref="WeaponClass"/> is still
    /// AUTHORED at the profile-build sites in <see cref="ShipCombatValueDB"/>; this classifier proves the axes + specs
    /// carry enough to DERIVE it, so a later slice can drop the authored field and make Class a pure read-out (a
    /// computed label, not a design choice). It is deliberately consistent with the authored mapping so
    /// <see cref="WeaponProfile.ComputedClass"/> == the authored <see cref="WeaponProfile.Class"/> for every real
    /// weapon (the invariant the unification rests on — gauged by <c>WeaponClassifierTests</c>).
    ///
    /// PURE + deterministic → unit-testable, no engine state. The class is a READOUT of how the weapon behaves in the
    /// dodge model, so the thresholds mirror the dodge references in <see cref="CombatEngagement"/>.
    /// </summary>
    public static class WeaponClassifier
    {
        // ── classification thresholds (flagged — tuning knobs, not gameplay numbers; they only pick the LABEL) ──
        /// <summary>At/above this shot velocity (m/s) a discrete projectile reads as a light-speed BEAM (undodgeable).
        /// Far above a railgun slug (~5e4) and the dodge reference (1e6), so only a genuine beam clears it.</summary>
        public const double BeamVelocityThreshold_mps = 10_000_000.0;

        /// <summary>At/above this saturation (tracks/sec) a discrete-projectile weapon reads as FLAK (its volume of
        /// fire floors the dodge). Mirrors <see cref="CombatEngagement.SaturationReference"/>.</summary>
        public const double FlakSaturationThreshold = 50.0;

        /// <summary>Derive the triangle corner from the DELIVERY (how it's thrown) plus the specs that disambiguate a
        /// discrete projectile. Delivery is the primary signal — it's literally the physics that meets the dodge; the
        /// velocity/saturation checks only split the Bolt/Slug family (a hyper-velocity slug behaves like a beam; a
        /// pellet storm behaves like flak). Never throws.</summary>
        public static WeaponClass Classify(WeaponDelivery delivery, double velocity, double tracking, double saturation)
        {
            switch (delivery)
            {
                case WeaponDelivery.Beam:   return WeaponClass.Beam;     // continuous, ~light-speed
                case WeaponDelivery.Guided: return WeaponClass.Missile;  // it tracks the target
                case WeaponDelivery.Cloud:  return WeaponClass.Flak;     // many projectiles = saturation
                case WeaponDelivery.Blast:  return WeaponClass.Missile;  // area effect → the heavy/guided bucket (v1)
                case WeaponDelivery.Bolt:
                case WeaponDelivery.Slug:
                default:
                    // A discrete shot: near-light-speed reads as a beam, a pellet storm as flak, else a ballistic slug.
                    if (velocity >= BeamVelocityThreshold_mps) return WeaponClass.Beam;
                    if (saturation >= FlakSaturationThreshold) return WeaponClass.Flak;
                    return WeaponClass.Railgun;
            }
        }
    }
}
