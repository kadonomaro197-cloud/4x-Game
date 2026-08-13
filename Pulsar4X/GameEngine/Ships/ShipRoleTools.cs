using Pulsar4X.Engine;
using Pulsar4X.Datablobs;       // ComponentInstancesDB — a built ship's installed parts
using Pulsar4X.Combat;          // ShipCombatValueDB — the built ship's firepower readout
using Pulsar4X.Weapons;         // the six weapon attributes (a weapon = a warship)
using Pulsar4X.GeoSurveys;      // GeoSurveyAtb  (a survey sensor)
using Pulsar4X.JumpPoints;      // GravSurveyAtb (a jump-point survey sensor)
using Pulsar4X.Logistics;       // LogiBaseAtb   (a logistics module → a freighter)
using Pulsar4X.Storage;         // CargoTransferAtb (tender) / CargoStorageAtb (hauler)
using Pulsar4X.GroundCombat;    // GroundBayAtb  (a troop bay → a transport)

namespace Pulsar4X.Ships
{
    /// <summary>
    /// What KIND of thing a ship is — warship, freighter, survey ship, and so on. This is the space twin of
    /// <see cref="Pulsar4X.GroundCombat.GroundRoleComposer"/>'s ground roles: a ship's job is NOT a label stuck on it,
    /// it EMERGES from the parts bolted to the hull. Strip a ship's guns and it stops reading as a warship; give a
    /// freighter a survey sensor and it reads as a survey ship. There is deliberately no stored "is this military?"
    /// flag (the engine HAS a dead <c>ShipInfoDB.IsMilitary</c> that is never set or read) — the class is DERIVED, live,
    /// from what the ship carries.
    /// </summary>
    public enum ShipRole
    {
        /// <summary>Carries a weapon — the only MILITARY class. Firepower &gt; 0.</summary>
        Warship,
        /// <summary>Carries a survey sensor (geological or jump-point). Civilian.</summary>
        Survey,
        /// <summary>Carries a logistics module (a trade-hub office) — an automated freighter node. Civilian.</summary>
        Freighter,
        /// <summary>Carries a troop bay — lifts ground units. Civilian HULL, military cargo.</summary>
        Transport,
        /// <summary>Carries cargo-TRANSFER gear (a fleet oiler/collier that refuels its mates in the field). Civilian.</summary>
        Tender,
        /// <summary>Carries a plain cargo HOLD (moves goods, but no transfer gear). Civilian.</summary>
        Hauler,
        /// <summary>None of the above — a bare utility hull. Civilian.</summary>
        Utility,
    }

    /// <summary>
    /// The ONE place that decides a ship's <see cref="ShipRole"/> from the components it carries. Both seats read it:
    /// the Forces window shows it as the ship's Class column, and the faction AI uses <see cref="IsWarship(ShipDesign)"/>
    /// to tell a warship from a freighter (the studio law — "one verb, both seats": if the window and the AI classified
    /// ships two different ways they would drift, so there is exactly one classifier and everything calls it).
    ///
    /// Two entry points, because a ship is classified at two moments: from a <see cref="ShipDesign"/> at DESIGN time
    /// (what the AI plans with — no ship is built yet), and from a live <see cref="Entity"/> once BUILT (what the window
    /// lists). Both read the same set of part-abilities in the same priority order, so they agree.
    ///
    /// Pure and defensive — it reads state and returns a value; it mutates nothing, throws nothing, and schedules
    /// nothing. It is safe to call every frame from the UI and every tick from the AI.
    /// </summary>
    public static class ShipRoleTools
    {
        /// <summary>
        /// A ship DESIGN is a WARSHIP if it mounts any weapon component. This is the exact predicate the faction AI
        /// used to duplicate in <c>ConquerResolver</c> and <c>DefendResolver</c>; both now delegate here so there is a
        /// single definition (One Verb, Both Seats). Any of the six weapon kinds counts.
        /// </summary>
        public static bool IsWarship(ShipDesign ship)
            => ship != null
            && (ship.TryGetComponentsByAttribute<GenericBeamWeaponAtb>(out _)
             || ship.TryGetComponentsByAttribute<RailgunWeaponAtb>(out _)
             || ship.TryGetComponentsByAttribute<FlakWeaponAtb>(out _)
             || ship.TryGetComponentsByAttribute<PlasmaBoltWeaponAtb>(out _)
             || ship.TryGetComponentsByAttribute<DisruptorWeaponAtb>(out _)
             || ship.TryGetComponentsByAttribute<MissileLauncherAtb>(out _));

        /// <summary>
        /// Classify a ship DESIGN (design time — what the AI plans with). Priority order (first match wins):
        /// weapon → Warship; survey sensor → Survey; logistics module → Freighter; troop bay → Transport;
        /// cargo-transfer gear → Tender; cargo hold → Hauler; else → Utility.
        /// </summary>
        public static ShipRole ClassifyRole(ShipDesign design)
        {
            if (design == null) return ShipRole.Utility;

            if (IsWarship(design)) return ShipRole.Warship;

            if (design.TryGetComponentsByAttribute<GeoSurveyAtb>(out _)
             || design.TryGetComponentsByAttribute<GravSurveyAtb>(out _)) return ShipRole.Survey;

            if (design.TryGetComponentsByAttribute<LogiBaseAtb>(out _)) return ShipRole.Freighter;

            if (design.TryGetComponentsByAttribute<GroundBayAtb>(out _)) return ShipRole.Transport;

            if (design.TryGetComponentsByAttribute<CargoTransferAtb>(out _)) return ShipRole.Tender;

            if (design.TryGetComponentsByAttribute<CargoStorageAtb>(out _)) return ShipRole.Hauler;

            return ShipRole.Utility;
        }

        /// <summary>
        /// Classify a BUILT ship (a live <see cref="Entity"/> — what the Forces window lists). Same priority order as
        /// the design classifier; the only difference is the WARSHIP test, which reads the built ship's
        /// <see cref="ShipCombatValueDB.Firepower"/> (the number the window's Strength column already shows — "a ship
        /// with combat firepower &gt; 0 is a warship"), falling back to a weapon-component scan if the ship has no
        /// combat-value blob yet. A ship with no <see cref="ComponentInstancesDB"/> is a bare Utility hull.
        /// </summary>
        public static ShipRole ClassifyRole(Entity ship)
        {
            if (ship == null) return ShipRole.Utility;

            // Warship: prefer the built firepower readout (matches the window Strength column + MilitaryComposition);
            // fall back to a weapon-component scan if the combat value hasn't been computed.
            if (ship.TryGetDataBlob<ShipCombatValueDB>(out var cv) && cv.Firepower > 0)
                return ShipRole.Warship;

            if (!ship.TryGetDataBlob<ComponentInstancesDB>(out var comps))
                return ShipRole.Utility;

            if (comps.TryGetComponentsByAttribute<GenericBeamWeaponAtb>(out _)
             || comps.TryGetComponentsByAttribute<RailgunWeaponAtb>(out _)
             || comps.TryGetComponentsByAttribute<FlakWeaponAtb>(out _)
             || comps.TryGetComponentsByAttribute<PlasmaBoltWeaponAtb>(out _)
             || comps.TryGetComponentsByAttribute<DisruptorWeaponAtb>(out _)
             || comps.TryGetComponentsByAttribute<MissileLauncherAtb>(out _)) return ShipRole.Warship;

            if (comps.TryGetComponentsByAttribute<GeoSurveyAtb>(out _)
             || comps.TryGetComponentsByAttribute<GravSurveyAtb>(out _)) return ShipRole.Survey;

            if (comps.TryGetComponentsByAttribute<LogiBaseAtb>(out _)) return ShipRole.Freighter;

            if (comps.TryGetComponentsByAttribute<GroundBayAtb>(out _)) return ShipRole.Transport;

            if (comps.TryGetComponentsByAttribute<CargoTransferAtb>(out _)) return ShipRole.Tender;

            if (comps.TryGetComponentsByAttribute<CargoStorageAtb>(out _)) return ShipRole.Hauler;

            return ShipRole.Utility;
        }

        /// <summary>
        /// Is this role a MILITARY one? Only <see cref="ShipRole.Warship"/> is — every other class is civilian
        /// (a troop transport is a civilian HULL carrying military cargo; the developer's default, Forces-window §7).
        /// This is the Mil/Civ filter the roster offers.
        /// </summary>
        public static bool IsMilitary(ShipRole role) => role == ShipRole.Warship;

        /// <summary>Convenience: is this built ship military? (warship)</summary>
        public static bool IsMilitary(Entity ship) => IsMilitary(ClassifyRole(ship));

        /// <summary>Convenience: is this design military? (warship)</summary>
        public static bool IsMilitary(ShipDesign design) => IsMilitary(ClassifyRole(design));
    }
}
