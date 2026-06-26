using System.Collections.Generic;
using Pulsar4X.Engine;
using Pulsar4X.Fleets;
using Pulsar4X.Ships;

namespace Pulsar4X.Sensors
{
    /// <summary>
    /// Read and set a fleet's EMCON posture (<see cref="FleetEmconDB"/>) — the run-hot/cruise/go-dark lever.
    /// Mirrors <c>Combat.FleetDoctrine</c>: read helpers plus a direct <see cref="SetPosture"/> setter (not an
    /// EntityCommand, so it's usable mid-combat). Setting a posture immediately pushes the resulting EMITTED
    /// signature scale onto every member ship's <see cref="SensorProfileDB.ActivityMultiplier"/> — the dial the
    /// detection math (<see cref="SensorTools.AttenuatedForDistance"/>) already reads (detection slice 3a). So:
    /// posture (fleet choice) → ship's ActivityMultiplier → emitted signal in the detection math → how far off you
    /// can be seen. That is the whole stack this slice wires.
    /// </summary>
    public static class FleetEmcon
    {
        // Posture → EMITTED-signature scale. Full = as-designed (no change); lower = quieter. These are the
        // gameplay-feel tunables (like Combat's SalvoDamageScale) — start sane, tune in live-test. Silent is not
        // zero: you can never be perfectly invisible (your hull still reflects an active ping, and a cold ship
        // still leaks SOME heat), so "go dark" cuts your loudness hard but doesn't erase you.
        public const double FullMultiplier = 1.0;
        public const double CruiseMultiplier = 0.5;
        public const double SilentMultiplier = 0.15;

        /// <summary>The EMITTED-signature scale a given posture implies.</summary>
        public static double MultiplierFor(EmconPosture posture) => posture switch
        {
            EmconPosture.Full => FullMultiplier,
            EmconPosture.Cruise => CruiseMultiplier,
            EmconPosture.Silent => SilentMultiplier,
            _ => FullMultiplier,
        };

        /// <summary>This fleet's active EMCON posture (Full if it has none set).</summary>
        public static EmconPosture PostureOf(Entity fleet)
            => fleet != null && fleet.TryGetDataBlob<FleetEmconDB>(out var d) ? d.Posture : EmconPosture.Full;

        /// <summary>This fleet's current EMITTED-signature scale from its posture (1.0 if it has none).</summary>
        public static double MultiplierOf(Entity fleet) => MultiplierFor(PostureOf(fleet));

        /// <summary>
        /// Set a fleet's EMCON posture and immediately apply it: every member ship's
        /// <see cref="SensorProfileDB.ActivityMultiplier"/> is set to the posture's signature scale, so the change
        /// shows up on the very next sensor scan. Direct call (not an order), like doctrine, so it works while the
        /// fleet is engaged. Recurses sub-fleets (fleet components) — the whole fleet goes dark together.
        ///
        /// v1 PUSH model: the multiplier is written onto ships here, the moment the player flips the switch (instant
        /// feedback). Folding in runtime activity (reactor load, thrust, weapons firing) is the next slices (3c+),
        /// which add a processor that recomputes ActivityMultiplier = posture × activity each tick; the posture set
        /// here is one input to that. A ship assigned to the fleet AFTER the posture is set keeps its own default
        /// until that processor (or a re-set) reconciles it — a flagged v1 gap, not a silent one.
        /// </summary>
        public static void SetPosture(Entity fleet, EmconPosture posture)
        {
            if (fleet == null) return;
            fleet.SetDataBlob(new FleetEmconDB(posture));
            double mult = MultiplierFor(posture);
            foreach (var ship in EnumerateShips(fleet))
                if (ship.TryGetDataBlob<SensorProfileDB>(out var profile))
                    profile.ActivityMultiplier = mult;
        }

        /// <summary>
        /// A fleet's live ships, recursing into sub-fleets (fleet components). Mirrors
        /// <c>CombatEngagement.GetFleetShips</c>, kept here so EMCON depends only on Fleets/Ships/Sensors — not on
        /// the combat resolver (clean layering: Combat → Sensors, never Sensors → Combat).
        /// </summary>
        private static IEnumerable<Entity> EnumerateShips(Entity fleet)
        {
            if (fleet == null || !fleet.IsValid || !fleet.TryGetDataBlob<FleetDB>(out var fleetDB)) yield break;
            foreach (var child in fleetDB.Children)
            {
                if (child == null || !child.IsValid) continue;
                if (child.HasDataBlob<ShipInfoDB>())
                    yield return child;
                else if (child.HasDataBlob<FleetDB>())
                    foreach (var sub in EnumerateShips(child))
                        yield return sub;
            }
        }
    }
}
