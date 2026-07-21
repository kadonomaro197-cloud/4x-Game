using System;
using System.Collections.Generic;
using Pulsar4X.Engine;
using Pulsar4X.Extensions;
using Pulsar4X.Factions;

namespace Pulsar4X.GroundCombat
{
    /// <summary>
    /// Ground-force UPKEEP — a standing ground unit costs its owning faction credits every month simply by EXISTING
    /// (the ground echo of <see cref="Pulsar4X.Stations.StationUpkeepProcessor"/>). This closes the litmus-test gap
    /// "a garrison sitting idle costs the empire nothing."
    ///
    /// It copies the station's monthly-bill pattern but does NOT add a processor: landmine L9 allows exactly ONE hotloop
    /// per DataBlob, and <see cref="GroundForcesProcessor"/> already owns <see cref="GroundForcesDB"/>. So the ground tick
    /// calls <see cref="BillIfDue"/> once per pass, and it bills only when a month has elapsed (a save-safe last-billed
    /// clock on <see cref="GroundForcesDB.LastUpkeepBilled"/>). Byte-identical: a unit's <see cref="GroundUnit.UpkeepCredits"/>
    /// defaults to 0, so a body of free units bills nobody. Defensive/no-throw — runs inside the ground hotloop (L4).
    ///
    /// Generalises to ANY unit: the dial lives on <see cref="GroundUnitDesign.UpkeepCredits"/>, snapshotted at raise;
    /// billing groups by <see cref="GroundUnit.FactionOwnerID"/> so a CONTESTED body correctly charges each side its own
    /// standing force. There is no unit-type special-case.
    /// </summary>
    public static class GroundUpkeep
    {
        /// <summary>Days between upkeep bills — matches <c>StationUpkeepProcessor</c>'s monthly (30-day) cadence.</summary>
        public const double BillIntervalDays = 30.0;

        /// <summary>
        /// Bill this body's standing ground-force upkeep IF a month has elapsed since the last bill. On the FIRST sight of
        /// a body it just stamps the clock (no back-bill from year 1). Then, once due, it groups the units by owning
        /// faction, sums each faction's Σ <see cref="GroundUnit.UpkeepCredits"/>, and books it as an expense on that
        /// faction's <c>Money</c> ledger under <see cref="Pulsar4X.Factions.TransactionCategory.GroundForceUpkeep"/>.
        /// No-op on a body with no roster / all-zero upkeep / no game. Never throws.
        /// </summary>
        public static void BillIfDue(Entity body, DateTime now)
        {
            try
            {
                if (body == null || !body.TryGetDataBlob<GroundForcesDB>(out var forces) || forces.Units == null) return;
                var game = body.Manager?.Game;
                if (game == null) return;

                // First sight: initialise the clock, don't back-bill from the epoch.
                if (forces.LastUpkeepBilled == default)
                {
                    forces.LastUpkeepBilled = now;
                    return;
                }
                if ((now - forces.LastUpkeepBilled).TotalDays < BillIntervalDays) return;
                forces.LastUpkeepBilled = now;

                // Sum upkeep per owning faction (a contested body holds two sides; skip neutral/unowned + free units).
                var costByFaction = new Dictionary<int, double>();
                foreach (var unit in forces.Units)
                {
                    if (unit == null || unit.FactionOwnerID < 0 || unit.UpkeepCredits <= 0) continue;
                    costByFaction.TryGetValue(unit.FactionOwnerID, out var c);
                    costByFaction[unit.FactionOwnerID] = c + unit.UpkeepCredits;
                }

                foreach (var kv in costByFaction)
                {
                    if (kv.Value <= 0) continue;
                    if (!game.Factions.TryGetValue(kv.Key, out var faction)) continue;
                    if (!faction.TryGetDataBlob<FactionInfoDB>(out var fi)) continue;
                    fi.Money.AddExpense(now, TransactionCategory.GroundForceUpkeep,
                        $"Ground-force upkeep on {body.GetName(kv.Key)}", (decimal)kv.Value);
                }
            }
            catch { /* upkeep is a nicety — never break the ground hotloop (L4) */ }
        }
    }
}
