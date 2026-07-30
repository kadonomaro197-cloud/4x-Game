using System.Collections.Generic;
using Pulsar4X.Components;   // ComponentDesign.GetAttribute<T>
using Pulsar4X.Datablobs;    // ComponentInstancesDB
using Pulsar4X.Movement;     // ⚠ PositionDB — the LIVE one. Engine/Datablobs/PositionDB.cs declares a second
                             //   class of the same name and is ENTIRELY COMMENTED OUT, so importing
                             //   Pulsar4X.Datablobs for it silently gets you nothing.
using Pulsar4X.Engine;
using Pulsar4X.Galaxy;       // MassVolumeDB

namespace Pulsar4X.Docking
{
    /// <summary>
    /// DOCKING — the read/write surface for berthing whole vessels inside a carrier.
    ///
    /// <para><b>Nothing calls this automatically.</b> No ship docks unless something asks it to, so a stock game is
    /// byte-identical by construction: no design carries a bay yet, and no processor reaches in here. What this gives
    /// the game is the <b>capability and its gates</b>; the order that lets a player or the AI ask for it is the next
    /// slice, and it will be ONE verb both seats issue (the developer's law) because it is one method here.</para>
    ///
    /// <para><b>The two gates ARE the gameplay.</b> A dock is not "more cargo space" — it is a fixed answer to
    /// <i>how many, and how big</i>. <see cref="Capacity"/> is the budget and <see cref="DockBayAtb.MaxHullMass"/> is
    /// the door; a design either fits through the door or it does not, however much spare tonnage is left.</para>
    ///
    /// <para><b>The consequence that makes it real:</b> a docked ship is re-parented to its carrier
    /// (<c>PositionDB.SetParent</c>, which preserves absolute position on the switch — the same mechanism that makes a
    /// moon follow its planet), so <b>it travels with the carrier and stops being independently located</b>. Undocking
    /// hands it back to whatever the carrier itself orbits. That is what separates a hangar from a spreadsheet row.</para>
    ///
    /// <para>Every method is defensive and never throws: these are reachable from order execution and, later, from a
    /// hotloop, where a throw kills the game clock (landmine L4).</para>
    /// </summary>
    public static class DockTools
    {
        /// <summary>Total berth capacity installed on <paramref name="carrier"/>, in kg of docked hull. Summed ON DEMAND
        /// from its <see cref="DockBayAtb"/> components (the fortification / GroundBayAtb pattern), scaled by component
        /// health — a shot-up bay holds less — so install/uninstall need no bookkeeping.</summary>
        public static double Capacity(Entity carrier)
        {
            if (carrier == null || !carrier.TryGetDataBlob<ComponentInstancesDB>(out var comps)) return 0;
            if (!comps.TryGetComponentsByAttribute<DockBayAtb>(out var bays)) return 0;

            double total = 0;
            foreach (var inst in bays)
            {
                var atb = inst.Design?.GetAttribute<DockBayAtb>();
                if (atb == null || !inst.IsEnabled) continue;
                total += atb.BerthTonnage * inst.HealthPercent;
            }
            return total;
        }

        /// <summary>The largest single vessel <paramref name="carrier"/> can admit, in kg — the widest DOOR it has, not
        /// the sum. Two small bays do not combine into one big one, which is the whole point of the per-item cap.</summary>
        public static double LargestBerth(Entity carrier)
        {
            if (carrier == null || !carrier.TryGetDataBlob<ComponentInstancesDB>(out var comps)) return 0;
            if (!comps.TryGetComponentsByAttribute<DockBayAtb>(out var bays)) return 0;

            double best = 0;
            foreach (var inst in bays)
            {
                var atb = inst.Design?.GetAttribute<DockBayAtb>();
                if (atb == null || !inst.IsEnabled) continue;
                double door = atb.MaxHullMass * inst.HealthPercent;
                if (door > best) best = door;
            }
            return best;
        }

        /// <summary>Mass of hull currently docked, in kg. Ids that no longer resolve (a docked ship destroyed by a hit)
        /// are skipped rather than throwing, so a dangling id costs capacity nothing.</summary>
        public static double Used(Entity carrier)
        {
            double used = 0;
            foreach (var ship in DockedShips(carrier)) used += HullMass(ship);
            return used;
        }

        /// <summary>Spare berth capacity in kg, never negative.</summary>
        public static double Free(Entity carrier)
        {
            double free = Capacity(carrier) - Used(carrier);
            return free < 0 ? 0 : free;
        }

        /// <summary>The vessels currently docked in <paramref name="carrier"/>, resolved from ids. Never null.</summary>
        public static List<Entity> DockedShips(Entity carrier)
        {
            var list = new List<Entity>();
            if (carrier == null || carrier.Manager == null) return list;
            if (!carrier.TryGetDataBlob<DockedShipsDB>(out var docked)) return list;

            foreach (int id in docked.DockedShipIds)
            {
                if (carrier.Manager.TryGetEntityById(id, out var ship) && ship != null && ship.IsValid)
                    list.Add(ship);
            }
            return list;
        }

        /// <summary>True if <paramref name="ship"/> is recorded as docked in <paramref name="carrier"/>.</summary>
        public static bool IsDockedIn(Entity carrier, Entity ship)
            => carrier != null && ship != null
               && carrier.TryGetDataBlob<DockedShipsDB>(out var d) && d.DockedShipIds.Contains(ship.Id);

        /// <summary>
        /// Both gates, as one readable answer. <paramref name="reason"/> says which gate refused, so the client and the
        /// AI get the same explanation rather than a bare false (the Visibility Gate: a refusal nobody can read is a bug
        /// report waiting to happen).
        /// </summary>
        public static bool CanDock(Entity carrier, Entity ship, out string reason)
        {
            reason = "";
            if (carrier == null || ship == null) { reason = "no carrier or no ship"; return false; }
            if (carrier.Id == ship.Id) { reason = "a ship cannot dock inside itself"; return false; }
            if (carrier.Manager != ship.Manager) { reason = "they are not in the same system"; return false; }
            if (IsDockedIn(carrier, ship)) { reason = "already docked"; return false; }

            double capacity = Capacity(carrier);
            if (capacity <= 0) { reason = "the carrier has no docking bay"; return false; }

            double mass = HullMass(ship);
            if (mass <= 0) { reason = "the ship has no mass to berth"; return false; }

            // GATE 1 — the door. Checked first because it is the one that never changes with load, so it gives the
            // clearer message: "too big" beats "no room" when the answer would be permanent either way.
            double door = LargestBerth(carrier);
            if (mass > door)
            {
                reason = $"too large for the biggest berth ({mass:N0} kg vs {door:N0} kg)";
                return false;
            }

            // GATE 2 — the budget.
            double free = Free(carrier);
            if (mass > free)
            {
                reason = $"not enough free berth ({mass:N0} kg needed, {free:N0} kg free)";
                return false;
            }
            return true;
        }

        /// <summary>
        /// Dock <paramref name="ship"/> inside <paramref name="carrier"/> if both gates pass. On success the ship is
        /// re-parented to the carrier, so <b>it travels with it</b> and is no longer independently located.
        /// </summary>
        public static bool TryDock(Entity carrier, Entity ship, out string reason)
        {
            if (!CanDock(carrier, ship, out reason)) return false;
            try
            {
                if (!carrier.TryGetDataBlob<DockedShipsDB>(out var docked))
                {
                    docked = new DockedShipsDB();
                    carrier.SetDataBlob(docked);
                }
                docked.DockedShipIds.Add(ship.Id);

                // The consequence: its position becomes relative to the carrier's. SetParent preserves the absolute
                // position across the switch, so nothing teleports — the same mechanism that makes a moon follow a planet.
                if (ship.TryGetDataBlob<PositionDB>(out var pos))
                    pos.SetParent(carrier);

                return true;
            }
            catch
            {
                reason = "docking failed";
                return false;
            }
        }

        /// <summary>
        /// Release one docked vessel, handing its position back to whatever the carrier itself is parented to (its SOI
        /// body), so it resumes being independently located from where the carrier currently is.
        /// </summary>
        public static bool Undock(Entity carrier, Entity ship)
        {
            if (carrier == null || ship == null) return false;
            if (!carrier.TryGetDataBlob<DockedShipsDB>(out var docked)) return false;
            if (!docked.DockedShipIds.Remove(ship.Id)) return false;

            try
            {
                if (ship.TryGetDataBlob<PositionDB>(out var pos)
                    && carrier.TryGetDataBlob<PositionDB>(out var carrierPos))
                {
                    pos.SetParent(carrierPos.Parent);   // back to the carrier's own SOI parent
                }
            }
            catch { /* the ship stays where it is rather than the clock dying */ }
            return true;
        }

        /// <summary>
        /// 🔒 THE GRAVE RUNG. Release everything aboard — what happens when the bay is shot off, or the carrier dies.
        /// Cradle-to-grave means the loss has to be expressible, and this is it: <b>a destroyed hangar sets its
        /// contents loose</b> rather than deleting them silently.
        /// </summary>
        public static int UndockAll(Entity carrier)
        {
            int n = 0;
            foreach (var ship in DockedShips(carrier))
                if (Undock(carrier, ship)) n++;
            return n;
        }

        /// <summary>A vessel's hull mass in kg — the currency both gates are denominated in, matching the Chassis
        /// door's ship mass budget. 0 when it cannot be read, which <see cref="CanDock"/> treats as a refusal.</summary>
        private static double HullMass(Entity ship)
        {
            if (ship == null) return 0;
            if (ship.TryGetDataBlob<MassVolumeDB>(out var mv)) return mv.MassTotal;
            return 0;
        }
    }
}
