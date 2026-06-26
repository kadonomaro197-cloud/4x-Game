using Newtonsoft.Json;
using Pulsar4X.Datablobs;

namespace Pulsar4X.Sensors
{
    /// <summary>
    /// EMCON (Emission Control) posture — how loud a fleet chooses to run. The naval EMCON-condition idea:
    /// one legible knob with three settings, from "all systems lit" to "go dark."
    ///   • <see cref="Full"/>   — emit as designed (default). Reactor up, full power, you light up the sky.
    ///   • <see cref="Cruise"/> — run cooler. A middle setting: less reach/power for a smaller signature.
    ///   • <see cref="Silent"/> — minimal emissions. Go dark to sneak — at the cost of running cold.
    /// The posture scales the fleet's ships' EMITTED signature via <see cref="SensorProfileDB.ActivityMultiplier"/>
    /// (the dial the detection math reads). It does NOT shrink the REFLECTED (radar-return) signature — going
    /// quiet doesn't shrink your hull, so an active radar still finds you.
    /// </summary>
    public enum EmconPosture
    {
        Full,
        Cruise,
        Silent,
    }

    /// <summary>
    /// A fleet's ACTIVE EMCON posture — the run-hot/cruise/go-dark choice the player (or NPC) has set for it.
    /// Mirrors <c>Combat.FleetDoctrineDB</c> exactly: a thin per-fleet DataBlob holding the active selection, set
    /// by a DIRECT call (<see cref="FleetEmcon.SetPosture"/>, not an order) so it stays available mid-combat —
    /// going dark while engaged is a valid tactical move, like changing doctrine. The posture is the persistent,
    /// save/loaded, UI-visible CHOICE; the per-ship effect (the signature scale) is derived from it and pushed
    /// onto each ship's <see cref="SensorProfileDB.ActivityMultiplier"/> by the setter.
    ///
    /// v1 stores only the posture (the multiplier is derived via <see cref="FleetEmcon.MultiplierFor"/> — one
    /// source of truth). A switch cooldown (a reactor can't flash-cool, so committing to dark would be a real
    /// tactical commitment) is a deliberate candidate follow-up, left out of v1 to keep the lever a single wire.
    /// </summary>
    public class FleetEmconDB : BaseDataBlob
    {
        [JsonProperty] public EmconPosture Posture { get; internal set; } = EmconPosture.Full;

        public FleetEmconDB() { }

        public FleetEmconDB(EmconPosture posture)
        {
            Posture = posture;
        }

        public FleetEmconDB(FleetEmconDB db)
        {
            Posture = db.Posture;
        }

        public override object Clone() => new FleetEmconDB(this);
    }
}
