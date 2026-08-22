using System;
using System.Collections.Generic;
using Pulsar4X.Colonies;
using Pulsar4X.Datablobs;
using Pulsar4X.Engine;
using Pulsar4X.Interfaces;
using Pulsar4X.People;

namespace GameEngine.People;

public class AdminSpaceProcessor : IInstanceProcessor
{
    internal override void ProcessEntity(Entity entity, DateTime atDateTime)
    {
        if(entity.TryGetDataBlob<AdminSpaceDB>(out var adminSpaceDB))
        {
            CalcEntityAdminSpace(entity, adminSpaceDB);

            // Update colony hex map if this is a colony
            if (entity.HasDataBlob<ColonyInfoDB>())
            {
                ColonyHexMapProcessor.ForceUpdateColonyHexMap(entity);
            }
        }
    }

    /// <summary>
    /// Rebuilds the admin-seat list from the entity's currently-installed admin components,
    /// PRESERVING any commander already seated. The previous version rebuilt the list from scratch
    /// on every pass, so the next processor tick silently un-seated every administrator — nothing
    /// downstream could ever hold an assignment. Seats are matched to components by name (the same
    /// key <see cref="Pulsar4X.People.Orders.AssignAdministratorOrder"/> uses). A seat whose component
    /// was removed drops out, and its occupant is unassigned so no dangling AssignedTo is left behind.
    /// </summary>
    internal static void CalcEntityAdminSpace(Entity entity, AdminSpaceDB adminSpaceDB)
    {
        var current = new List<(AdminLevel level, string name)>();
        if (entity.GetDataBlob<ComponentInstancesDB>().TryGetComponentsByAttribute<AdminSpaceAtb>(out var adminSpaces))
        {
            foreach (var adminSpace in adminSpaces)
            {
                var attributes = adminSpace.GetAttributes();
                var atb = (AdminSpaceAtb)attributes[typeof(AdminSpaceAtb)];
                current.Add((atb.AdminLevel, adminSpace.Name));
            }
        }

        adminSpaceDB.CommanderSeats = ReconcileSeats(adminSpaceDB.CommanderSeats, current);
    }

    /// <summary>
    /// Pure seat reconciliation (unit-testable with no game scaffolding): produce the seat list for the
    /// given installed components while carrying over each existing seat — and the commander sitting in
    /// it — whose component is still present, matched by component name. Existing seats are reused BY
    /// REFERENCE so a seated commander survives the recalc. Any previous seat whose component is gone is
    /// dropped, and its occupant's <see cref="Pulsar4X.People.CommanderDB.AssignedTo"/> is cleared.
    /// </summary>
    internal static List<AdminSpaceAbilityState> ReconcileSeats(
        List<AdminSpaceAbilityState> previous,
        List<(AdminLevel level, string name)> current)
    {
        previous ??= new List<AdminSpaceAbilityState>();
        var result = new List<AdminSpaceAbilityState>();
        var carried = new HashSet<AdminSpaceAbilityState>();

        foreach (var (level, name) in current)
        {
            AdminSpaceAbilityState? existing = null;
            foreach (var prev in previous)
            {
                if (!carried.Contains(prev) && prev.ComponentName == name)
                {
                    existing = prev;
                    break;
                }
            }

            if (existing != null)
            {
                // Match is by ComponentName (identity — carries the seated officer across a recalc), but APPLY the
                // component's current AdminLevel to the carried seat so a redesigned/retiered command component's scope
                // can't go stale. This is the "use the level, not just the name" half of finishing the AdminLevel dial:
                // the seat's SeatType is what CanOfficerHoldSeat / AssignAdministratorOrder.IsValidCommand gate on.
                existing.SeatType = level;
                carried.Add(existing);
                result.Add(existing);
            }
            else
            {
                result.Add(new AdminSpaceAbilityState(level, name));
            }
        }

        // Components that were removed: their seats drop out; free the occupant so no dangling assignment.
        foreach (var prev in previous)
        {
            if (!carried.Contains(prev) && prev.Commander != null)
            {
                prev.Commander.AssignedTo = -1;
            }
        }

        return result;
    }

    /// <summary>
    /// Drop the (first) seat provided by a named command component and free any commander sitting in it —
    /// the decapitation grave rung, called when that admin component is uninstalled or destroyed. Keyed by
    /// component name (the same key seats are built and assigned by). Pure/list-level → unit-testable with
    /// no game scaffolding. Returns true if a seat was dropped.
    /// </summary>
    internal static bool DropSeatForComponent(List<AdminSpaceAbilityState> seats, string componentName)
    {
        if (seats == null)
            return false;

        for (int i = 0; i < seats.Count; i++)
        {
            if (seats[i].ComponentName == componentName)
            {
                if (seats[i].Commander != null)
                    seats[i].Commander.AssignedTo = -1;
                seats.RemoveAt(i);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Vacate the seat currently held by the given commander (e.g. the officer was killed) so no dead
    /// commander stays "seated". Returns true if a seat was vacated. Pure → unit-testable.
    /// </summary>
    internal static bool VacateSeat(AdminSpaceDB adminSpaceDB, int commanderId)
    {
        if (adminSpaceDB == null)
            return false;

        foreach (var seat in adminSpaceDB.CommanderSeats)
        {
            if (seat.CommanderID == commanderId)
            {
                seat.CommanderID = -1;
                seat.Commander = null;
                return true;
            }
        }

        return false;
    }

    // ── v1 SPAN-OF-CONTROL: AdminLevel is now READ by a real gate (Operation Blueprint-to-Steel, "wire it") ──
    // AdminLevel used to be carried onto each seat as a label that NO rule read. The gate below turns it into a real
    // decision: a broader command scope needs a more SENIOR officer, so the AdminLevel you built into a command seat
    // now bounds WHO can be seated there. Pure/unit-testable; consumed by AssignAdministratorOrder behind its flag.

    /// <summary>How many RANK points a seat's AdminLevel is discounted before it starts requiring seniority. With the
    /// default 5, every routine post (Ship … Colony, ordinals 0–5) is UNGATED (required rank 0 = any officer), and only
    /// the broader STRATEGIC scopes need a progressively more senior officer: Planet 1, SOI 2, System 3, Sector 4,
    /// Empire 5. ⚠ FLAGGED tunable — start officers are rank 1–6 (<see cref="CommanderFactory"/>), so this shape lets a
    /// green officer run a colony but reserves an empire-wide command for a senior one. Move to per-level JSON once tuned.</summary>
    public static int AdminRankLevelOffset = 5;   // FLAGGED

    /// <summary>v1 SPAN-OF-CONTROL RANK GATE (pure): the minimum officer <see cref="CommanderDB.Rank"/> required to hold
    /// a command seat of the given <see cref="AdminLevel"/>. Monotonic — a broader scope demands a more senior officer.
    /// = <c>max(0, (int)level − <see cref="AdminRankLevelOffset"/>)</c>, so routine posts are ungated (0) and only the
    /// strategic scopes bite. Pure/unit-testable.</summary>
    public static int AdminRankRequired(AdminLevel seatLevel)
    {
        int req = (int)seatLevel - AdminRankLevelOffset;
        return req < 0 ? 0 : req;
    }

    /// <summary>Can this officer hold a command seat of the given <see cref="AdminLevel"/>? True iff the commander
    /// exists and their <see cref="CommanderDB.Rank"/> clears <see cref="AdminRankRequired"/> — the rule
    /// <see cref="Pulsar4X.People.Orders.AssignAdministratorOrder"/> reads when its rank gate is on. Pure — a null
    /// commander can hold nothing.</summary>
    public static bool CanOfficerHoldSeat(CommanderDB commander, AdminLevel seatLevel)
        => commander != null && commander.Rank >= AdminRankRequired(seatLevel);
}