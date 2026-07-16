using System.Collections.Generic;
using Pulsar4X.Combat;
using Pulsar4X.Engine;
using Pulsar4X.Names;   // NameDB (parent name for the sub-fleet label)
using Pulsar4X.Ships;   // ShipInfoDB (move ships only, never a nested sub-fleet node)

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

        /// <summary>
        /// Actually FORMS the sub-fleets: for each fighting job that at least one ship plays, creates a child fleet
        /// under <paramref name="parentFleet"/>, moves that job's ships into it, sets its flagship, and tags it with a
        /// <see cref="FleetRoleDB"/> marker. Returns the role → new-sub-fleet map (empty if the fleet is null, has no
        /// <see cref="FleetDB"/>, isn't in a manager, or has no direct ships to sort).
        ///
        /// This is the step that turns the plan from <see cref="PlanRoleSubFleets"/> into real structure — the
        /// boarding party actually told off by station. It runs pure engine-side (this class is in the GameEngine
        /// assembly, so it calls the internal tree mutators directly — the same way <c>ColonyFactory</c> builds the
        /// start fleet, not through player orders).
        ///
        /// It sorts ONLY the parent's DIRECT ship children (skipping any ship already nested in an existing sub-fleet),
        /// so detaching a ship always targets the node that actually holds it — no ship ends up in two lists. Re-bucketing
        /// an already-nested tree is a later-slice job. Nothing in the game loop calls this yet, so a running game is
        /// byte-identical; a later slice wires it into the AI's battle-prep and gives each sub-fleet its own doctrine.
        /// </summary>
        public static Dictionary<FleetRole, Entity> FormRoleSubFleets(Entity parentFleet)
        {
            var formed = new Dictionary<FleetRole, Entity>();
            if (parentFleet == null || !parentFleet.TryGetDataBlob<FleetDB>(out var parentDB))
                return formed;

            var manager = parentFleet.Manager;      // the ships' StarSystem — co-locate the sub-fleets here
            if (manager == null) return formed;

            int factionId = parentFleet.FactionOwnerID;
            string parentName = parentFleet.TryGetDataBlob<NameDB>(out var pn) ? pn.DefaultName : "Fleet";

            // Bucket ONLY the parent's DIRECT ship children (GetChildren() returns a snapshot, so mutating the tree
            // below is safe). Skip existing sub-fleet nodes and non-ships — we move real ships whose real parent IS
            // this fleet, so the detach below can't miss.
            var byRole = new Dictionary<FleetRole, List<Entity>>();
            foreach (var child in parentDB.GetChildren())
            {
                if (child == null || !child.IsValid) continue;
                if (child.HasDataBlob<FleetDB>()) continue;       // a sub-fleet node — leave it
                if (!child.HasDataBlob<ShipInfoDB>()) continue;   // ships only
                var role = ClassifyRole(child);
                if (!byRole.TryGetValue(role, out var list)) byRole[role] = list = new List<Entity>();
                list.Add(child);
            }

            foreach (var kv in byRole)
            {
                var ships = kv.Value;
                if (ships.Count == 0) continue;

                var sub = FleetFactory.Create(manager, factionId, $"{parentName} — {kv.Key}");
                var subDB = sub.GetDataBlob<FleetDB>();
                subDB.SetParent(parentFleet);                     // make the sub-fleet a child of the parent
                sub.SetDataBlob(new FleetRoleDB(kv.Key));         // tag it with its role (the marker B-2c reads)

                bool first = true;
                foreach (var ship in ships)
                {
                    parentDB.RemoveChild(ship);                   // detach from the flat parent
                    if (first)
                    {
                        subDB.FlagShipID = ship.Id;               // first ship is the sub-fleet flagship
                        ship.Manager.Transfer(sub);               // co-locate (no-op: sub already in this manager)
                        first = false;
                    }
                    subDB.AddChild(ship);                         // attach to the sub-fleet
                }
                formed[kv.Key] = sub;
            }

            return formed;
        }
    }
}
