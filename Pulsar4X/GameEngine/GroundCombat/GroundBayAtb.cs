using Newtonsoft.Json;
using Pulsar4X.Interfaces;
using Pulsar4X.Components;
using Pulsar4X.Engine;
using Pulsar4X.Datablobs;

namespace Pulsar4X.GroundCombat
{
    /// <summary>The two ways a ground unit rides a ship (the developer's call, 2026-07-05): foot soldiers ride a
    /// <see cref="GroundCarryClass.Personnel"/> troop bay; vehicles (armour, artillery) ride a
    /// <see cref="GroundCarryClass.Vehicle"/> cargo bay. A bay only accepts its own class — you can't cram a tank into
    /// a troop bay — and how MANY fit is a size calc (bay capacity vs each unit's carry-size), not a fixed slot count.</summary>
    public enum GroundCarryClass : byte
    {
        Personnel,   // foot soldiers — ride a troop bay
        Vehicle      // armour / artillery — ride a cargo bay
    }

    /// <summary>
    /// TROOP/VEHICLE BAY gear on a buildable SHIP component — the attribute that makes a component able to CARRY ground
    /// units off-world (the transport half of "you can take a planet"). A ship with a bay can load units from a colony,
    /// fly them to an enemy world, and land them (slice T1b). This is the lift capacity in the invasion chain:
    /// build army → LOAD onto a transport → win the orbit → LAND → fight → take the planet.
    ///
    /// Two stats: <see cref="Capacity"/> (how much carry-room the bay has, in carry-size units) and
    /// <see cref="CarryClass"/> (Personnel = troops, Vehicle = armour/artillery — a bay only carries its own class).
    /// A unit loads if the bay's remaining room ≥ that unit's carry-size, so a bigger unit eats more room.
    ///
    /// A component attribute (<see cref="IComponentDesignAttribute"/>) so it rides the normal research/design/build/
    /// install/save rails (CONVENTIONS §6). Capacity is summed ON DEMAND by <c>GroundTransport</c> (like fortification
    /// and population support), so the install/uninstall hooks are no-ops. **Grave rung:** a bay shot off a ship in
    /// transit takes the units aboard with it (T1b/T4). Design: docs/GROUND-COMBAT-MAP-DESIGN.md → transport.
    /// </summary>
    public class GroundBayAtb : BaseDataBlob, IComponentDesignAttribute
    {
        /// <summary>Carry-room this bay provides, in the same carry-size units a loaded unit consumes.</summary>
        [JsonProperty] public double Capacity { get; internal set; }
        /// <summary>Which class of unit it carries (Personnel troops vs Vehicle armour/artillery).</summary>
        [JsonProperty] public GroundCarryClass CarryClass { get; internal set; } = GroundCarryClass.Personnel;

        public GroundBayAtb() { }

        // double args — the JSON binder feeds AtbConstrArgs(PropertyValue(...)) values as doubles (NCalc), so the ctor
        // must accept doubles for the base-mod component to bind (gotcha L7). 0 = Personnel, 1 = Vehicle.
        public GroundBayAtb(double capacity, double carryClass)
        {
            Capacity = capacity < 0 ? 0 : capacity;
            CarryClass = (GroundCarryClass)(int)carryClass;
        }

        public override object Clone() => new GroundBayAtb(Capacity, (double)(int)CarryClass);

        public void OnComponentInstallation(Entity parentEntity, ComponentInstance componentInstance) { }
        public void OnComponentUninstallation(Entity parentEntity, ComponentInstance componentInstance) { }

        public string AtbName() => CarryClass == GroundCarryClass.Vehicle ? "Vehicle Bay" : "Troop Bay";
        public string AtbDescription()
            => $"Carries ground {(CarryClass == GroundCarryClass.Vehicle ? "vehicles (armour/artillery)" : "troops (infantry)")} — {Capacity:0} carry-room. Load units at a colony, win the orbit, and land them on a target world.";
    }
}
