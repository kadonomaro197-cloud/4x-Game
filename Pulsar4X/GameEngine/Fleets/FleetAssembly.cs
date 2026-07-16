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

        /// <summary>This faction's own fleet-composition ladder — the per-faction numbers authored on its
        /// <see cref="Pulsar4X.Factions.FactionInfoDB"/> (a militarist BATTLE-LINE runs bigger fleets than an
        /// expansionist RAID-SWARM), falling back to the shared <see cref="DefaultTemplate"/> (3/8/18) for a faction
        /// with no FactionInfoDB (byte-identical for the engine tests, which build factions with the default numbers).
        /// The whole composition + reserve logic reads the ladder back off each fleet's <see cref="FleetCompositionDB"/>,
        /// so stamping it here at fleet creation is the SINGLE choke point that gives each faction its own strike size.</summary>
        public static FleetCompositionTemplate TemplateFor(Entity faction)
        {
            if (faction != null && faction.TryGetDataBlob<Pulsar4X.Factions.FactionInfoDB>(out var info))
                return new FleetCompositionTemplate(info.FleetTemplateName, info.FleetMinToDeploy, info.FleetIdealSize, info.FleetPerfectSize);
            return DefaultTemplate;
        }

        /// <summary>Treasury at/above which the AI aims to grow a fleet to its IDEAL size (slice 3). Anchored below
        /// <c>NeedsLadder.AmbitionWealth</c> (100k) — "some money in the bank" buys the ideal configuration. FLAGGED tunable.</summary>
        public const decimal IdealWealth = 50000m;

        /// <summary>Treasury at/above which the AI aims for the PERFECT (largest) size — "plentiful resources" (slice 3).
        /// FLAGGED tunable.</summary>
        public const decimal PerfectWealth = 150000m;

        /// <summary>
        /// Slice 3 — the resource-gated ASPIRATION: how big should the AI grow its fleet given the money it has and
        /// whether it's under pressure? Pure function of the two inputs (the reads — treasury off the Ledger, war off
        /// DiplomacyDB — live in the AI policy caller): broke → just the Deployable core; some money → Ideal; plentiful
        /// → Perfect. WAR bumps the aim up ONE tier ("IF … the situation calls for it" — the developer's rule), so a
        /// battered faction still fields a real fighting fleet. Never returns Forming (the aim is at least Deployable).
        /// </summary>
        public static FleetCompositionTier AspirationFor(decimal balance, bool atWar)
        {
            var tier = balance >= PerfectWealth ? FleetCompositionTier.Perfect
                     : balance >= IdealWealth   ? FleetCompositionTier.Ideal
                     :                             FleetCompositionTier.Deployable;
            if (atWar) tier = BumpOneTier(tier);   // the situation calls for it → aim higher
            return tier;
        }

        private static FleetCompositionTier BumpOneTier(FleetCompositionTier tier) => tier switch
        {
            FleetCompositionTier.Deployable => FleetCompositionTier.Ideal,
            FleetCompositionTier.Ideal      => FleetCompositionTier.Perfect,
            _                               => FleetCompositionTier.Perfect,   // already Perfect (or Forming, which we never pass)
        };

        /// <summary>Stamp the chosen <paramref name="aspiration"/> onto every one of the faction's FORMING fleets
        /// (the fleets tagged <see cref="FleetCompositionDB"/>), so the warship-massing rung reads the same target back.
        /// Pure engine read+set; the treasury/war DECISION lives in the AI policy caller (<see cref="AspirationFor"/>).</summary>
        public static void SetAspiration(Entity faction, FleetCompositionTier aspiration)
        {
            if (faction == null) return;
            int factionId = faction.Id;
            var game = faction.Manager?.Game;
            if (game == null) return;
            foreach (var system in game.Systems)
            {
                if (system == null) continue;
                foreach (var fleet in system.GetAllEntitiesWithDataBlob<FleetCompositionDB>())
                    if (fleet.FactionOwnerID == factionId
                        && fleet.TryGetDataBlob<FleetCompositionDB>(out var comp))
                        comp.Aspiration = aspiration;
            }
        }

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
                    var template = TemplateFor(faction);          // this faction's own ladder (battle-line vs raid-swarm), else the default
                    fleet = FleetFactory.Create(manager, factionId, template.Name);
                    fleetDB = fleet.GetDataBlob<FleetDB>();
                    fleetDB.SetParent(faction);                   // a TOP-LEVEL fleet (child of the faction root → shows in OwnedFleets + the Fleet window)
                    comp = new FleetCompositionDB(template);
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

        /// <summary>The faction-owned FORMING fleet in this system with the MOST ships but still below its ASPIRATION
        /// target (concentrate force — fill one fleet to its resourced size before starting another), skipping any fleet
        /// locked in a battle. Null if none qualifies → the caller starts a fresh forming fleet. **Slice 3 — the reserve
        /// seam:** capping growth at the aspiration target (not PerfectSize) is what makes the OVERFLOW form a SECOND
        /// fleet: once fleet #1 is at its target, the next hulls start fleet #2 (the home-defense RESERVE the military
        /// commander keeps back), instead of piling into one ever-growing blob that gets committed whole.</summary>
        private static Entity FindGrowableFormingFleet(EntityManager manager, int factionId)
        {
            Entity best = null;
            int bestCount = -1;
            foreach (var fleet in manager.GetAllEntitiesWithDataBlob<FleetCompositionDB>())
            {
                if (fleet.FactionOwnerID != factionId) continue;
                if (fleet.HasDataBlob<FleetCombatStateDB>()) continue;         // don't reinforce a fleet mid-battle
                if (!fleet.TryGetDataBlob<FleetCompositionDB>(out var comp)) continue;
                int target = comp.Template.TargetCountFor(comp.Aspiration);
                if (target <= 0) target = comp.PerfectSize;                    // defensive: unset/Forming aspiration → grow to perfect
                int count = ArmedShipCount(fleet);
                if (count >= target) continue;                                 // this fleet is COMPLETE for its aspiration → overflow starts a NEW fleet (the reserve)
                if (count > bestCount) { bestCount = count; best = fleet; }
            }
            return best;
        }

        /// <summary>Count the armed ships (a real <see cref="ShipCombatValueDB"/> with Firepower &gt; 0) under a fleet,
        /// recursing any sub-fleets — the same measure <c>MilitaryComposition.WarshipCount</c> uses. Internal so the
        /// fleet-tree-safety gauge can prove it terminates on a malformed tree (it rides the guarded FleetCombat.Ships).</summary>
        internal static int ArmedShipCount(Entity fleet)
        {
            int n = 0;
            foreach (var ship in FleetCombat.Ships(fleet))
                if (ship != null && ship.IsValid && ship.TryGetDataBlob<ShipCombatValueDB>(out var cv) && cv.Firepower > 0)
                    n++;
            return n;
        }
    }
}
