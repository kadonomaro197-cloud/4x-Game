using Pulsar4X.Engine;
using Pulsar4X.Datablobs;   // ComponentInstancesDB (namespace ≠ folder)
using Pulsar4X.Movement;    // PositionDB — the ACTIVE class lives here, not in Datablobs (see Tests/CLAUDE.md)
using Pulsar4X.Components;
using Pulsar4X.Ships;

namespace Pulsar4X.GroundCombat
{
    /// <summary>
    /// The TRANSPORT mechanics — moving a ground unit off its home world, across space on a ship, and down onto a
    /// (possibly enemy) world. The middle link of "you can take a planet": build army → <see cref="TryLoadUnit"/> onto a
    /// ship with a <see cref="GroundBayAtb"/> bay → fly it there (the existing fleet move orders) → win the orbit →
    /// <see cref="TryLandUnit"/>. Capacity is size-based (a bay of a given class holds units whose carry-sizes sum to its
    /// capacity), and landing is gated on orbital control (T1b: no enemy ship over the target). All primitives are
    /// defensive — they return false rather than throw. Design: docs/GROUND-COMBAT-MAP-DESIGN.md → transport.
    ///
    /// v1 carry-class and carry-size are a function of UNIT TYPE (a unit is a battalion-sized chunk, not a soldier);
    /// when the designer gains a unit-strength knob, carry-size scales with that instead (a bigger unit eats more room).
    /// </summary>
    public static class GroundTransport
    {
        // ── carry classification (v1: by unit type) ──────────────────────────────────────────────────────────────
        // NUMBERS TO REVIEW: infantry are Personnel (troop bay); armour/artillery are Vehicles (cargo bay).
        public static GroundCarryClass CarryClassOf(GroundUnitType type)
            => type == GroundUnitType.Infantry ? GroundCarryClass.Personnel : GroundCarryClass.Vehicle;

        // NUMBERS TO REVIEW (flagged): carry-room a single unit consumes, by type. Infantry small, artillery mid,
        // armour large — a bigger unit takes more bay room. These become strength-scaled once the designer has a size knob.
        public static double CarrySizeOf(GroundUnitType type) => type switch
        {
            GroundUnitType.Infantry  => 1.0,
            GroundUnitType.Artillery => 2.0,
            GroundUnitType.Armor     => 3.0,
            _ => 1.0,
        };
        public static double CarrySizeOf(GroundUnit unit) => unit == null ? 0 : CarrySizeOf(unit.UnitType);

        // ── capacity (summed on demand from installed bays) ──────────────────────────────────────────────────────
        /// <summary>Total carry-room of the given class installed on the ship (summed from its <see cref="GroundBayAtb"/> bays).</summary>
        public static double BayCapacity(Entity ship, GroundCarryClass carryClass)
        {
            double cap = 0;
            if (ship != null && ship.TryGetDataBlob<ComponentInstancesDB>(out var comps)
                && comps.TryGetComponentsByAttribute<GroundBayAtb>(out var bays))
            {
                foreach (var inst in bays)
                {
                    if (inst?.Design == null) continue;
                    var atb = inst.Design.GetAttribute<GroundBayAtb>();
                    if (atb != null && atb.CarryClass == carryClass) cap += atb.Capacity;
                }
            }
            return cap;
        }

        /// <summary>Carry-room of the given class already used by units aboard the ship.</summary>
        public static double UsedCapacity(Entity ship, GroundCarryClass carryClass)
        {
            double used = 0;
            if (ship != null && ship.TryGetDataBlob<GroundTransportDB>(out var t))
                foreach (var u in t.LoadedUnits)
                    if (CarryClassOf(u.UnitType) == carryClass) used += CarrySizeOf(u);
            return used;
        }

        /// <summary>Free carry-room of the given class on the ship.</summary>
        public static double FreeCapacity(Entity ship, GroundCarryClass carryClass)
            => BayCapacity(ship, carryClass) - UsedCapacity(ship, carryClass);

        /// <summary>True if the ship has a bay of the unit's class with room for its carry-size.</summary>
        public static bool CanLoad(Entity ship, GroundUnit unit)
        {
            if (ship == null || unit == null) return false;
            var cls = CarryClassOf(unit.UnitType);
            return FreeCapacity(ship, cls) >= CarrySizeOf(unit);
        }

        // ── load / land ──────────────────────────────────────────────────────────────────────────────────────────
        /// <summary>Load <paramref name="unit"/> (standing on <paramref name="body"/>) onto <paramref name="ship"/>: the
        /// ship must be AT the body and have a bay of the unit's class with room. Moves the unit OUT of the body's roster
        /// into the ship's bays (identity + health preserved). Returns false if any check fails. Never throws.</summary>
        public static bool TryLoadUnit(Entity ship, Entity body, GroundUnit unit)
        {
            if (ship == null || body == null || unit == null) return false;
            if (!ShipIsAtBody(ship, body)) return false;
            if (!body.TryGetDataBlob<GroundForcesDB>(out var forces) || !forces.Units.Contains(unit)) return false;
            if (!CanLoad(ship, unit)) return false;

            forces.Units.Remove(unit);
            // clear any in-flight march state — it's aboard now, not walking
            unit.MovingToRegion = -1; unit.TransitSecondsRemaining = 0;
            unit.HexPath = null; unit.GlobalPath = null;

            var transport = EnsureTransport(ship);
            transport.LoadedUnits.Add(unit);
            return true;
        }

        /// <summary>Land <paramref name="unit"/> (aboard <paramref name="ship"/>) onto <paramref name="targetBody"/>'s
        /// region <paramref name="regionIndex"/>: the ship must be at the body AND hold the orbit (no enemy ship present).
        /// Moves the unit OUT of the ship's bays onto the body's roster via <see cref="GroundForces.PlaceExistingUnit"/>
        /// (keeps its health; a hostile drop lands it right into the region's fight). Returns false if any check fails.
        /// Never throws.</summary>
        public static bool TryLandUnit(Entity ship, Entity targetBody, GroundUnit unit, int regionIndex)
        {
            if (ship == null || targetBody == null || unit == null) return false;
            if (!ship.TryGetDataBlob<GroundTransportDB>(out var transport) || !transport.LoadedUnits.Contains(unit)) return false;
            if (!ShipIsAtBody(ship, targetBody)) return false;
            if (!HasOrbitalControl(ship, targetBody)) return false;

            transport.LoadedUnits.Remove(unit);
            GroundForces.PlaceExistingUnit(targetBody, unit, regionIndex);
            // AI formation parity (R2 gap 3): a landed invader is swept into a BATTALION with the faction's other loose
            // units on this world, so the ground tactical brain commands a formation rather than a lone loose unit.
            // Gated OFF by default → landing is byte-identical (the AutoRaiseHomeGarrison pattern) until the flag is flipped.
            if (GroundAssembly.AutoFormUp)
                GroundAssembly.FormUpLoose(targetBody, unit.FactionOwnerID);
            return true;
        }

        // ── orbital control ──────────────────────────────────────────────────────────────────────────────────────
        /// <summary>True if <paramref name="ship"/>'s faction holds the orbit over <paramref name="targetBody"/> — i.e.
        /// no ship of another (non-neutral) faction is present at the body. You must win the space battle before you can
        /// land troops (v1: presence-based; a diplomacy-aware "hostile only" refinement comes later). Never throws.</summary>
        public static bool HasOrbitalControl(Entity ship, Entity targetBody)
        {
            if (ship == null || targetBody?.Manager == null) return false;
            int invader = ship.FactionOwnerID;
            foreach (var e in targetBody.Manager.GetAllEntitiesWithDataBlob<ShipInfoDB>())
            {
                if (e.FactionOwnerID == invader) continue;
                if (e.FactionOwnerID == Game.NeutralFactionId) continue;
                if (ShipIsAtBody(e, targetBody)) return false;   // a foreign ship holds the orbit
            }
            return true;
        }

        // ── helpers ──────────────────────────────────────────────────────────────────────────────────────────────
        /// <summary>True if the ship is positioned AT the body (its position parent is that body — i.e. it orbits it).</summary>
        public static bool ShipIsAtBody(Entity ship, Entity body)
        {
            if (ship == null || body == null) return false;
            return ship.TryGetDataBlob<PositionDB>(out var pos) && pos.Parent != null && pos.Parent.Id == body.Id;
        }

        private static GroundTransportDB EnsureTransport(Entity ship)
        {
            if (!ship.TryGetDataBlob<GroundTransportDB>(out var t))
            {
                t = new GroundTransportDB();
                ship.SetDataBlob(t);
            }
            return t;
        }
    }
}
