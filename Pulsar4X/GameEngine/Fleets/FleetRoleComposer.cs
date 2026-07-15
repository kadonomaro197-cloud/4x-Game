using System.Collections.Generic;
using Pulsar4X.Combat;
using Pulsar4X.Engine;

namespace Pulsar4X.Fleets
{
    /// <summary>
    /// The four fighting jobs a ship does inside a fleet — the sub-fleet's *role* (Q7: a fleet is made of sub-fleets
    /// that each play a part, on top of the fleet's own posture). Think of a boarding party told off by station:
    /// the fast movers form the SCREEN out front, the gunline is the LINE, the long-reach guns sit back as ARTILLERY,
    /// and the tenders/tankers are SUPPORT, kept out of the shooting. This is the vocabulary the AI (and later the
    /// player) uses to say "these ships fight up close, those ones stand off."
    /// </summary>
    public enum FleetRole
    {
        /// <summary>Fast, nimble ships that lead — pickets, fighters, corvettes. Sorted by high evasion.</summary>
        Screen,
        /// <summary>The gunline — ordinary warships that trade blows at the main range. The default fighting ship.</summary>
        Line,
        /// <summary>Long-reach ships that fire from the back — railguns, missiles. Sorted by weapon reach.</summary>
        Artillery,
        /// <summary>Non-combatants kept out of the fight — tankers, transports, tenders. Utility hulls.</summary>
        Support,
    }

    /// <summary>
    /// Sorts a fleet's ships into the four <see cref="FleetRole"/> jobs by reading the combat numbers each ship
    /// already carries (its <see cref="ShipCombatValueDB"/>). This is a pure *read* — it looks at the ships and
    /// buckets them; it does NOT touch the fleet tree, issue orders, or change any state. It's the first step of the
    /// sub-fleet plan: decide who plays what part. A later slice takes this bucketing and actually forms the
    /// sub-fleets and hands each its own doctrine.
    ///
    /// Because it mutates nothing and nothing in the game loop calls it yet, adding this class leaves a running game
    /// byte-for-byte identical.
    /// </summary>
    public static class FleetRoleComposer
    {
        /// <summary>A ship this evasive or better is fast enough to ride out front as the Screen.</summary>
        public const double ScreenEvasionThreshold = 0.5;

        /// <summary>A weapon reaching this far or farther makes a ship a stand-off Artillery piece (railgun/missile
        /// class). Beam (~5 km) and flak (~50 km) fall well short, so a knife-fighter isn't miscast as artillery.</summary>
        public const double ArtilleryRangeThreshold = 400_000;

        /// <summary>
        /// Which job does this one ship do? Reads its <see cref="ShipCombatValueDB"/> and decides, in priority order:
        /// a utility hull (RoleWeight below 1.0 — a tanker/transport) is <see cref="FleetRole.Support"/>; a nimble
        /// ship (evasion at or above the screen threshold) leads as <see cref="FleetRole.Screen"/>; a long-reach ship
        /// (a weapon at or beyond the artillery range) stands off as <see cref="FleetRole.Artillery"/>; everything
        /// else is the gunline, <see cref="FleetRole.Line"/>. A ship with no combat value at all (an unrated hull)
        /// counts as Support — it isn't a fighting ship.
        /// </summary>
        public static FleetRole ClassifyRole(Entity ship)
        {
            if (ship == null || !ship.TryGetDataBlob<ShipCombatValueDB>(out var cv))
                return FleetRole.Support;

            if (cv.RoleWeight < 1.0)           return FleetRole.Support;   // utility hull — kept out of the fight
            if (cv.Evasion >= ScreenEvasionThreshold) return FleetRole.Screen;      // fast mover — leads
            if (cv.MaxWeaponRange >= ArtilleryRangeThreshold) return FleetRole.Artillery; // long reach — stands off
            return FleetRole.Line;                                          // ordinary warship — the gunline
        }

        /// <summary>
        /// Groups every ship in a fleet (recursing existing sub-fleets, via <see cref="FleetCombat.Ships"/>) by the
        /// <see cref="FleetRole"/> it plays. Every one of the four roles is present as a key (empty lists for roles no
        /// ship fills), so a caller can iterate the four jobs without null checks. Pure read — no tree change, no order.
        /// </summary>
        public static Dictionary<FleetRole, List<Entity>> PlanRoleSubFleets(Entity fleet)
        {
            var buckets = new Dictionary<FleetRole, List<Entity>>
            {
                { FleetRole.Screen,    new List<Entity>() },
                { FleetRole.Line,      new List<Entity>() },
                { FleetRole.Artillery, new List<Entity>() },
                { FleetRole.Support,   new List<Entity>() },
            };

            if (fleet == null) return buckets;

            foreach (var ship in FleetCombat.Ships(fleet))
                buckets[ClassifyRole(ship)].Add(ship);

            return buckets;
        }
    }
}
