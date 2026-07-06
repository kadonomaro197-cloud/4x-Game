using System;

namespace Pulsar4X.GroundCombat
{
    /// <summary>
    /// The AMMO pool logic for a planetary unit (weapon-unification B) — PURE + resolver-agnostic, so it survives the
    /// combat-resolver merge (the merged resolver calls the same helper). The developer's ammo model (2026-07-06):
    /// magazines are mass-based, ammo depletes as the unit fires, and DRY silences the unit's ammo weapons (flak/railgun)
    /// while it keeps fighting with any energy/melee weapons. A resupply order refills it.
    ///
    /// This slice is the DURABLE pool + operations (snapshot at raise, consume, dry-check, refill). The in-combat DRAIN
    /// CALL SITE (fire → <see cref="Consume"/>) and the silence-when-dry read ride the resolver, which is being unified
    /// next branch — so they land there rather than being baked into the soon-replaced ground resolver. Ammo-per-shot is
    /// a WEAPON-DESIGNER spec (the kg a burst consumes), added when universal weapons feed the merged resolver. Never throws.
    /// </summary>
    public static class GroundAmmo
    {
        /// <summary>Does this unit carry an ammo pool at all (i.e. has a magazine)?</summary>
        public static bool CarriesAmmo(GroundUnit unit) => unit != null && unit.MaxAmmo_kg > 0;

        /// <summary>True when a unit that HAS ammo weapons has run dry — its ammo weapons go silent (it fights on with
        /// energy/melee). A unit with no magazine is never "dry" (it has nothing to run out of).</summary>
        public static bool IsDry(GroundUnit unit) => unit != null && unit.MaxAmmo_kg > 0 && unit.CurrentAmmo_kg <= 0;

        /// <summary>Fraction of the magazine remaining (0..1). 1 for a unit with no ammo pool (nothing to deplete).</summary>
        public static double Fraction(GroundUnit unit)
            => unit == null || unit.MaxAmmo_kg <= 0 ? 1.0 : Math.Max(0.0, Math.Min(1.0, unit.CurrentAmmo_kg / unit.MaxAmmo_kg));

        /// <summary>Drain <paramref name="kg"/> of ammo (a burst of fire). Floors at 0. Returns the amount ACTUALLY
        /// consumed (0 if the unit has no pool or is already dry) — so the caller can tell whether the burst was fed.</summary>
        public static double Consume(GroundUnit unit, double kg)
        {
            if (unit == null || kg <= 0 || unit.MaxAmmo_kg <= 0) return 0;
            double taken = Math.Min(unit.CurrentAmmo_kg, kg);
            if (taken <= 0) return 0;
            unit.CurrentAmmo_kg -= taken;
            if (unit.CurrentAmmo_kg < 0) unit.CurrentAmmo_kg = 0;
            return taken;
        }

        /// <summary>Top a unit's ammo back to full — a resupply. Returns the kg added.</summary>
        public static double Refill(GroundUnit unit)
        {
            if (unit == null || unit.MaxAmmo_kg <= 0) return 0;
            double added = unit.MaxAmmo_kg - unit.CurrentAmmo_kg;
            if (added < 0) added = 0;
            unit.CurrentAmmo_kg = unit.MaxAmmo_kg;
            return added;
        }
    }
}
