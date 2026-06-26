using System;
using Pulsar4X.Engine;
using Pulsar4X.Interfaces;
using Pulsar4X.Movement;
using Pulsar4X.Ships;
using Pulsar4X.Weapons;

namespace Pulsar4X.Sensors
{
    /// <summary>
    /// Makes a ship's EMITTED signature respond to what it's DOING — the heat half of EMCON. Posture
    /// (<see cref="FleetEmcon"/>) sets a baseline loudness; this processor modulates around it: a ship running its
    /// drive at full burn or firing its guns lights up far brighter than one coasting cold. It is the engine half
    /// of the developer's heat-asymmetry picture — "a ship at full burn passing while I'm on minimal life support,
    /// I see them long before they see me."
    ///
    /// Each run (every <see cref="RunFrequency"/>), for every ship it sets
    ///   <c>SensorProfileDB.ActivityMultiplier = SignatureBaseMultiplier × HeatFactor(burning, firing)</c>
    /// where <see cref="SensorProfileDB.SignatureBaseMultiplier"/> is the posture base (pushed by the lever) and
    /// the heat factor rises with activity. The detection math
    /// (<see cref="SensorTools.AttenuatedForDistance"/>) already reads <c>ActivityMultiplier</c> (slice 3a), so the
    /// louder signature shrinks how far off you can hide. An important emergent property: a lit drive plume can
    /// betray you even on a Silent posture — you can't burn quietly (Silent base 0.15 × burning ~5 ≈ 0.75).
    ///
    /// Keyed to <see cref="Pulsar4X.Ships.ShipInfoDB"/> — NOT SensorProfileDB, which <c>SensorReflectionProcessor</c>
    /// already owns (the engine allows ONE hotloop processor per DataBlob type — a second on the same type throws
    /// at startup registration, the same reason the battle trigger keys to StarInfoDB not FleetDB). ShipInfoDB is
    /// the right key anyway: only ships have an EMCON posture + activity, so it auto-scopes to ships, and planets/
    /// stars (no ShipInfoDB) are never touched — their signature stays as-is.
    ///
    /// NOTE — reactor load is deliberately NOT a heat input here yet. The obvious field
    /// (<c>EnergyGenAbilityDB.Load</c>) is buggy: its formula is <c>TotalOutputMax / batteryInflow</c> — inverted
    /// and unbounded (1.0 at idle, growing without bound under demand), NOT the "percent of max output" its own
    /// comment claims. Folding reactor load in needs either a fix to that field or a clean <c>Demand/TotalOutputMax</c>;
    /// flagged for a follow-up so this slice doesn't propagate the bug. Thrust + firing are the dominant, clean signals.
    /// </summary>
    public class EmconActivityProcessor : IHotloopProcessor
    {
        /// <summary>Heat added while the drive is lit (thrusting). The dominant signal — a burning ship is the
        /// loudest thing in the sky. 4.0 ⇒ ~5× louder ⇒ detected ~2.2× farther (inverse-square). The KEY feel
        /// tunable — bump it up to make a drive plume betray you harder.</summary>
        public const double ThrustHeat = 4.0;

        /// <summary>Heat added while firing weapons ("you can't shoot quietly"). 1.0 ⇒ ~2× louder while the guns
        /// are hot. A briefer spike than thrust; tunable.</summary>
        public const double WeaponHeat = 1.0;

        /// <summary>
        /// The activity heat factor (1.0 = cold/idle). Pure function of the two activity flags, so it's the unit
        /// under test. <c>HeatFactor(false,false)=1</c>; thrust and firing each add their heat and stack.
        /// </summary>
        public static double HeatFactor(bool burning, bool firing)
            => 1.0 + (burning ? ThrustHeat : 0.0) + (firing ? WeaponHeat : 0.0);

        /// <summary>The final EMITTED scale = posture base × activity heat. Pure (takes the inputs), so the
        /// posture×activity composition is testable without standing up a real ship.</summary>
        public static double ComputeActivityMultiplier(double signatureBase, bool burning, bool firing)
            => signatureBase * HeatFactor(burning, firing);

        /// <summary>Is this ship's drive currently lit? A ship thrusts exactly while it has a
        /// <see cref="NewtonMoveDB"/> with maneuver delta-V left to spend (the gate on thrust in the movement
        /// integrator). No flag needed — the burn state is already in the move blob. Defensive: false if not moving.</summary>
        public static bool IsBurning(Entity ship)
            => ship.TryGetDataBlob<NewtonMoveDB>(out var nm) && nm.ManuverDeltaVLen > 0;

        /// <summary>Did this ship fire any weapon in the most recent weapons tick? Reads the per-weapon
        /// <see cref="GenericFiringWeaponsDB.ShotsFiredThisTick"/> the firing processor sets. Defensive: false if
        /// the ship has no firing kit. (Transient — a ship firing intermittently may read cold between polls;
        /// sustained combat fire stays hot. Tunable/refine to a decaying "weapon heat" if it matters.)</summary>
        public static bool IsFiring(Entity ship)
        {
            if (!ship.TryGetDataBlob<GenericFiringWeaponsDB>(out var w)) return false;
            foreach (var shots in w.ShotsFiredThisTick)
                if (shots > 0) return true;
            return false;
        }

        public TimeSpan RunFrequency => TimeSpan.FromSeconds(5); // combat-relevant cadence (matches the battle trigger)

        public TimeSpan FirstRunOffset => TimeSpan.FromSeconds(1);

        public Type GetParameterType => typeof(ShipInfoDB);

        public void Init(Game game) { }

        public void ProcessEntity(Entity entity, int deltaSeconds)
        {
            // Keyed to ShipInfoDB, so every entity here is a ship. A ship that somehow lacks a signature profile
            // is simply skipped (defensive — no throw, the rule for any hotloop processor).
            if (!entity.TryGetDataBlob<SensorProfileDB>(out var profile)) return;

            profile.ActivityMultiplier =
                ComputeActivityMultiplier(profile.SignatureBaseMultiplier, IsBurning(entity), IsFiring(entity));
        }

        public int ProcessManager(EntityManager manager, int deltaSeconds)
        {
            var entities = manager.GetAllEntitiesWithDataBlob<ShipInfoDB>();
            foreach (var entity in entities)
                ProcessEntity(entity, deltaSeconds);
            return entities.Count;
        }
    }
}
