using System;
using System.Collections.Generic;
using Pulsar4X.Engine;
using Pulsar4X.Galaxy;

namespace Pulsar4X.GroundCombat
{
    /// <summary>
    /// DS-HAULER — the "make a supply convoy a LOSABLE ground unit" primitive (OPERATION BLUEPRINT-TO-STEEL slice
    /// DS-hauler-unit). Today the standing haul route (DS-T1, <see cref="GroundHaulRoute"/>) and the one-shot
    /// <see cref="Pulsar4X.Galaxy.HexHaulOrder"/> move ore between hexes as PURE BOOKKEEPING — the ore teleports, nothing
    /// can be intercepted. This helper makes a real unit carry the ore: LOAD it from a hex stockpile onto a
    /// <see cref="GroundUnit"/> (<see cref="LoadFromHex"/>), the unit MARCHES it over real distance on the EXISTING move
    /// path (the cargo rides for free as the unit's <see cref="GroundUnit.MineralCargo"/> field — nothing in the move
    /// code touches it), UNLOAD at the destination (<see cref="UnloadToHex"/>) — and, the whole point, if the hauler is
    /// KILLED mid-haul its load is STRANDED onto the hex it died on (<see cref="StrandOnDeath"/>), recoverable, instead
    /// of vanishing or teleporting home.
    ///
    /// The stockpile math is NOT re-implemented here — every move goes through the SAME conserved core the hex
    /// stockpile already exposes (<see cref="GroundHex.RemoveFromStockpile"/> / <see cref="GroundHex.AddToStockpile"/>):
    /// a hex gains exactly what it loses (or the unit gains exactly what the hex loses), capped by what's on hand, no
    /// ore created or destroyed. The unit's cargo dict and the hex stockpile share the SAME <c>ICargoable.ID</c> key
    /// space (see <see cref="GroundUnit.MineralCargo"/>), so there is no translation.
    ///
    /// Every method is DEFENSIVE and NEVER THROWS — the strand hook runs inside the ground hotloop's casualty step
    /// (landmine L4), so a null hex / no grid / an empty load is a quiet no-op, never an exception. <b>Byte-identical
    /// by construction</b>: nothing in the engine populates a unit's cargo on any existing path, so
    /// <see cref="StrandOnDeath"/> is a no-op for every cargo-less unit (i.e. every unit in a stock game) — no flag
    /// needed. Design: docs/ground/UNITS-ON-THE-MAP-DESIGN.md §3.
    /// </summary>
    public static class GroundHauler
    {
        /// <summary>Total units of all goods a unit is currently carrying (0 for a null/empty load). The carry-cap gauge.</summary>
        public static long TotalCargo(GroundUnit unit)
        {
            if (unit?.MineralCargo == null) return 0;
            long sum = 0;
            foreach (var kv in unit.MineralCargo) if (kv.Value > 0) sum += kv.Value;
            return sum;
        }

        /// <summary>Units of <paramref name="cargoableId"/> this unit is carrying (0 if none). Read helper.</summary>
        public static long CargoOf(GroundUnit unit, int cargoableId)
            => unit?.MineralCargo != null && unit.MineralCargo.TryGetValue(cargoableId, out var n) ? n : 0;

        /// <summary>
        /// LOAD ore from a hex stockpile onto the hauler unit — the "pick up the convoy's load" verb. Moves up to
        /// <paramref name="amount"/> (≤ 0 = ALL on hand) of <paramref name="cargoableId"/> from <paramref name="hex"/>'s
        /// stockpile onto the unit's <see cref="GroundUnit.MineralCargo"/>, CONSERVED (the unit gains exactly what the
        /// hex loses) and capped by what's actually present. If <paramref name="carryCap"/> &gt; 0 it also caps by the
        /// unit's remaining carry capacity (across its WHOLE load), so a unit can't carry past its limit; ≤ 0 = no cap.
        /// Returns the amount actually loaded (0 on a null hex/unit, an empty source, or a full unit). Never throws.
        /// </summary>
        public static long LoadFromHex(GroundHex hex, GroundUnit unit, int cargoableId, long amount, long carryCap = 0)
        {
            if (hex == null || unit == null) return 0;
            long onHand = hex.StockpileOf(cargoableId);
            if (onHand <= 0) return 0;
            long want = amount <= 0 ? onHand : Math.Min(amount, onHand);
            if (carryCap > 0)   // optional unit carry cap: never load past what the unit can still carry
            {
                long room = carryCap - TotalCargo(unit);
                if (room <= 0) return 0;
                want = Math.Min(want, room);
            }
            if (want <= 0) return 0;
            long taken = hex.RemoveFromStockpile(cargoableId, want);   // conserved + capped by the shared hex core
            if (taken <= 0) return 0;
            AddToCargo(unit, cargoableId, taken);
            return taken;
        }

        /// <summary>
        /// UNLOAD the hauler's WHOLE load onto a hex stockpile — the "drop the convoy's load at the depot" verb (and the
        /// shared core of <see cref="StrandOnDeath"/>). Moves every good the unit carries onto <paramref name="hex"/>'s
        /// stockpile, CONSERVED (the hex gains exactly what the unit was carrying), then empties the unit's cargo.
        /// Returns the total units dropped (0 on a null hex/unit or an empty load). Never throws.
        /// </summary>
        public static long UnloadToHex(GroundHex hex, GroundUnit unit)
        {
            if (hex == null || unit == null || unit.MineralCargo == null || unit.MineralCargo.Count == 0) return 0;
            long moved = 0;
            foreach (var kv in unit.MineralCargo)   // read the unit's dict, write the HEX's dict — no concurrent mutation
            {
                if (kv.Value <= 0) continue;
                hex.AddToStockpile(kv.Key, kv.Value);
                moved += kv.Value;
            }
            unit.MineralCargo.Clear();   // the load has left the unit
            return moved;
        }

        /// <summary>
        /// STRAND-ON-DEATH — the GRAVE RUNG. Drop a killed hauler's whole load onto the hex it died on, so the ore is
        /// recoverable where the convoy fell instead of teleporting home (or vanishing with the unit). Identical to
        /// <see cref="UnloadToHex"/> — a death is just an involuntary unload at the current hex. Returns the units
        /// stranded (0 on a null hex/unit or a cargo-less unit → byte-identical for every unit that carries nothing).
        /// Never throws.
        /// </summary>
        public static long StrandOnDeath(GroundHex hex, GroundUnit unit) => UnloadToHex(hex, unit);

        /// <summary>
        /// STRAND-ON-DEATH (body overload for the casualty hook) — resolve the unit's CURRENT hex on the body's global
        /// surface grid (where the stockpile lives, the same grid the haul routes / <see cref="Pulsar4X.Galaxy.HexHaulOrder"/>
        /// address) from its <see cref="GroundUnit.GlobalQ"/>/<see cref="GroundUnit.GlobalR"/>, then drop its load there.
        /// A cargo-less unit returns immediately (no grid work) → byte-identical. If the unit isn't on the grid or the
        /// body has no grid, it's a no-op (only a degenerate/test body has no grid). Never throws (L4).
        /// </summary>
        public static long StrandOnDeath(Entity body, GroundUnit unit)
        {
            if (body == null || unit == null) return 0;
            if (unit.MineralCargo == null || unit.MineralCargo.Count == 0) return 0;   // cargo-less → nothing to strand
            var hex = CurrentHex(body, unit);
            if (hex == null) return 0;
            return UnloadToHex(hex, unit);
        }

        /// <summary>Resolve the surface-grid hex a unit stands on (its <see cref="GroundUnit.GlobalQ"/>/<c>GlobalR</c> on
        /// the body's cylinder <c>SurfaceGrid</c> — the SAME grid the hex stockpile lives on). Builds the grid on demand.
        /// null if the unit isn't placed on the grid or the body has no grid. Never throws.</summary>
        private static GroundHex CurrentHex(Entity body, GroundUnit unit)
        {
            try
            {
                if (unit.GlobalQ < 0 || unit.GlobalR < 0) return null;
                var grid = PlanetGridFactory.EnsureGridForBody(body);
                if (grid == null || grid.Cols <= 0) return null;
                return grid.HexAt(unit.GlobalQ, unit.GlobalR);   // HexAt wraps the column, returns null for a bad row
            }
            catch { return null; }
        }

        /// <summary>Add units of a good to a unit's cargo (creating the dict on demand). A non-positive amount is a no-op —
        /// the mirror of <see cref="GroundHex.AddToStockpile"/>.</summary>
        private static void AddToCargo(GroundUnit unit, int cargoableId, long amount)
        {
            if (amount <= 0) return;
            unit.MineralCargo ??= new Dictionary<int, long>();
            unit.MineralCargo[cargoableId] = (unit.MineralCargo.TryGetValue(cargoableId, out var n) ? n : 0) + amount;
        }
    }
}
