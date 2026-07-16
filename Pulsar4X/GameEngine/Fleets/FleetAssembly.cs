using System.Collections.Generic;
using Pulsar4X.Combat;   // ShipCombatValueDB, FleetCombatStateDB, FleetCombat
using Pulsar4X.Engine;
using Pulsar4X.Ships;    // ShipInfoDB

namespace Pulsar4X.Fleets
{
    /// <summary>
    /// Fleet-composition slice 2 — the AI assembles its BUILT warships into ONE holistic FORMING fleet.
    ///
    /// The gap this closes: when an NPC finishes building a warship, <c>ShipDesign.OnConstructionComplete</c> parks it
    /// as a flat child of the faction's ROOT fleet (the FleetDB on the faction entity, which lives in the GlobalManager,
    /// not a star system). But the AI's offensive logic (<c>MilitaryComposition.ReadyStrikeFleet</c>) only sees
    /// <c>FleetState.OwnedFleets()</c> — real in-SYSTEM FleetDB entities — so a warship stuck in the root fleet is
    /// invisible, and the strike fleet never masses (it never reaches the 3-armed-ship threshold). Ships just pile up
    /// unfleeted.
    ///
    /// <see cref="AssembleBuiltWarships"/> fixes that: each monthly cycle it sweeps the faction's loose warships out of
    /// the root fleet and folds them into a real, faction-parented, flagship-bearing in-system fleet — GROWING an
    /// existing under-strength FORMING fleet before starting a new one (the developer's ladder: build toward ONE mass
    /// fleet, don't trickle). The fleet is tagged with a <see cref="FleetCompositionDB"/> memo so the next sweep finds
    /// it, and it flips to Deployable the moment it crosses the min holistic core.
    ///
    /// Pure engine-side tree mutation — the exact <see cref="FleetRoleComposer.FormRoleSubFleets"/> recipe
    /// (RemoveChild/SetParent/AddChild + FlagShipID, the way ColonyFactory builds the start fleet, NOT player orders).
    /// The NPC processor calls it ONLY behind <c>EnableOrderEmission</c>, so a default game / engine test is byte-identical.
    /// </summary>
    public static class FleetAssembly
    {
        /// <summary>The composition ladder a newly-assembled AI strike fleet climbs — the developer's holistic 3/8/18
        /// (min-to-deploy / ideal / perfect). Slice 3 gives a faction its own template; v1 shares this one.</summary>
        public static readonly FleetCompositionTemplate DefaultTemplate = FleetCompositionTemplate.DefaultStrikeFleet;

        /// <summary>
        /// Fold a faction's built-but-unfleeted WARSHIPS (the armed ships parked flat under its ROOT fleet) into a real
        /// in-system fleet per star system — growing an existing FORMING fleet before starting a new one. Returns the
        /// number of ships moved into fleets this call (0 = nothing loose to assemble).
        ///
        /// Only ARMED hulls (a <see cref="ShipCombatValueDB"/> with Firepower &gt; 0) are swept up — transports,
        /// freighters and surveyors are left loose for their own logic (the invasion transport the ConquerResolver
        /// builds must NOT get vacuumed into the strike fleet). Defensive/no-throw (runs inside a hotloop).
        /// </summary>
        public static int AssembleBuiltWarships(Entity faction)
        {
            if (faction == null || !faction.TryGetDataBlob<FleetDB>(out var rootDB)) return 0;
            int factionId = faction.Id;   // a faction entity's own Id is its faction identity (what FleetFactory.Create + OwnedFleets use)

            // Bucket the loose ARMED ships (flat children of the root fleet) by their star-system manager, so each
            // system's new hulls fold into a fleet co-located with them. GetChildren() returns a snapshot copy, so the
            // RemoveChild below is safe while nothing iterates the live list.
            var looseByManager = new Dictionary<EntityManager, List<Entity>>();
            foreach (var child in rootDB.GetChildren())
            {
                if (child == null || !child.IsValid) continue;
                if (child.HasDataBlob<FleetDB>()) continue;       // a named sub-fleet node, not a loose ship
                if (!child.HasDataBlob<ShipInfoDB>()) continue;   // ships only
                if (!child.TryGetDataBlob<ShipCombatValueDB>(out var cv) || cv.Firepower <= 0) continue; // armed hulls only
                var mgr = child.Manager;
                if (mgr == null) continue;
                if (!looseByManager.TryGetValue(mgr, out var list)) looseByManager[mgr] = list = new List<Entity>();
                list.Add(child);
            }

            int moved = 0;
            foreach (var kv in looseByManager)
            {
                var manager = kv.Key;
                var ships = kv.Value;
                if (ships.Count == 0) continue;

                // Grow an existing under-strength FORMING fleet in THIS system before starting a new one.
                var fleet = FindGrowableFormingFleet(manager, factionId);
                FleetDB fleetDB;
                FleetCompositionDB comp;
                if (fleet == null)
                {
                    fleet = FleetFactory.Create(manager, factionId, DefaultTemplate.Name);
                    fleetDB = fleet.GetDataBlob<FleetDB>();
                    fleetDB.SetParent(faction);                   // a TOP-LEVEL fleet (child of the faction root → shows in OwnedFleets + the Fleet window)
                    comp = new FleetCompositionDB(DefaultTemplate);
                    fleet.SetDataBlob(comp);                      // the memo that finds this fleet next sweep + tracks the deploy latch
                }
                else
                {
                    fleetDB = fleet.GetDataBlob<FleetDB>();
                    comp = fleet.GetDataBlob<FleetCompositionDB>();
                }

                foreach (var ship in ships)
                {
                    rootDB.RemoveChild(ship);                     // detach from the flat root fleet
                    if (fleetDB.FlagShipID < 0)
                    {
                        fleetDB.FlagShipID = ship.Id;             // first ship is the flagship (MoveToSystemBodyOrder needs FlagShipID != -1)
                        ship.Manager.Transfer(fleet);             // co-locate (no-op: fleet already created in this manager)
                    }
                    fleetDB.AddChild(ship);                       // attach to the forming fleet
                    moved++;
                }

                // DEPLOY transition (latched once): the first time the fleet crosses the min holistic core it becomes
                // Deployable. v1 flips the STATE only — the standing PATROL order rides slice 3 (escalation), where the
                // destination + re-issue belong; and ConquerResolver already SAILS a massed fleet the instant the
                // faction is at war with a reachable target, so the assembled fleet is usable without a patrol order here.
                if (!comp.Deployed && comp.Template.TierFor(ArmedShipCount(fleet)) != FleetCompositionTier.Forming)
                    comp.Deployed = true;
            }
            return moved;
        }

        /// <summary>The faction-owned FORMING fleet in this system with the MOST ships but still below its PerfectSize
        /// (concentrate force — fill one fleet before starting another), skipping any fleet locked in a battle. Null if
        /// none qualifies (→ the caller starts a fresh forming fleet).</summary>
        private static Entity FindGrowableFormingFleet(EntityManager manager, int factionId)
        {
            Entity best = null;
            int bestCount = -1;
            foreach (var fleet in manager.GetAllEntitiesWithDataBlob<FleetCompositionDB>())
            {
                if (fleet.FactionOwnerID != factionId) continue;
                if (fleet.HasDataBlob<FleetCombatStateDB>()) continue;         // don't reinforce a fleet mid-battle
                if (!fleet.TryGetDataBlob<FleetCompositionDB>(out var comp)) continue;
                int count = ArmedShipCount(fleet);
                if (count >= comp.PerfectSize) continue;                       // already perfect — leave it, start a new one
                if (count > bestCount) { bestCount = count; best = fleet; }
            }
            return best;
        }

        /// <summary>Count the armed ships (a real <see cref="ShipCombatValueDB"/> with Firepower &gt; 0) under a fleet,
        /// recursing any sub-fleets — the same measure <c>MilitaryComposition.WarshipCount</c> uses.</summary>
        private static int ArmedShipCount(Entity fleet)
        {
            int n = 0;
            foreach (var ship in FleetCombat.Ships(fleet))
                if (ship != null && ship.IsValid && ship.TryGetDataBlob<ShipCombatValueDB>(out var cv) && cv.Firepower > 0)
                    n++;
            return n;
        }
    }
}
