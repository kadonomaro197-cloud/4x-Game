using System;
using System.Collections.Generic;
using Pulsar4X.Colonies;
using Pulsar4X.Datablobs;
using Pulsar4X.Engine;
using Pulsar4X.Interfaces;

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
}