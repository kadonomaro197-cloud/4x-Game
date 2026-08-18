using System;
using GameEngine.People;
using Pulsar4X.Engine;
using Pulsar4X.Engine.Orders;
using Pulsar4X.Events;
using Pulsar4X.People;

namespace Pulsar4X.People.Orders;

public class AssignAdministratorOrder : EntityCommand
{
    public override ActionLaneTypes ActionLanes => ActionLaneTypes.InstantOrder;

    public override bool IsBlocking => false;

    public override string Name => "Assign Administrator to Post";

    public override string Details => "Instantly assigns an administrator to an admin post";

    /// <summary>v1 SPAN-OF-CONTROL gate (Operation Blueprint-to-Steel — "wire it", 2026-08-18). When ON, an officer may
    /// only be seated in a command post their RANK qualifies them for — a broader <see cref="AdminLevel"/> scope needs a
    /// more senior officer (<see cref="AdminSpaceProcessor.CanOfficerHoldSeat"/>). This is the first rule to READ the
    /// AdminLevel a command seat carries. Default OFF → every assignment is valid → byte-identical; leave it off until
    /// the rank map (<see cref="AdminSpaceProcessor.AdminRankLevelOffset"/>) is tuned against the live officer scale
    /// (the developer's call — flip to true in the client to activate). A post/officer that doesn't resolve falls
    /// through to VALID (Execute already no-ops on it), so ONLY a genuine under-ranked assignment is refused.</summary>
    public static bool EnableAdminRankGate = false;

    internal override Entity EntityCommanding => _adminEntity;

    private Entity _adminEntity;
    private int _administratorId;
    private string _postComponentName;

    private AssignAdministratorOrder(Entity adminEntity, int administratorId, string postComponentName)
    {
        _adminEntity = adminEntity;
        _administratorId = administratorId;
        _postComponentName = postComponentName;
    }

    public static AssignAdministratorOrder Create(Entity adminEntity, int administratorId, string postComponentName)
    {
        return new AssignAdministratorOrder(adminEntity, administratorId, postComponentName);
    }

    public override EntityCommand Clone()
    {
        throw new NotImplementedException();
    }

    internal override void Execute(DateTime atDateTime)
    {
        if(!_adminEntity.TryGetDataBlob<AdminSpaceDB>(out var adminSpaceDB))
            return;

        // Find the specific admin post by component name
        AdminSpaceAbilityState? post = null;
        foreach (var seat in adminSpaceDB.CommanderSeats)
        {
            if (seat.ComponentName == _postComponentName)
            {
                post = seat;
                break;
            }
        }

        if (post == null)
            return;

        if(!_adminEntity.Manager.TryGetGlobalEntityById(_administratorId, out var administrator))
            return;

        if(!administrator.TryGetDataBlob<CommanderDB>(out var commanderDB))
            return;

        // Need to find the current assignment and unassign them
        if(commanderDB.AssignedTo >= 0)
        {
            if(_adminEntity.Manager.TryGetGlobalEntityById(commanderDB.AssignedTo, out var previousEntity))
            {
                var unassignOrder = UnassignAdministratorOrder.Create(previousEntity, administrator.Id, _postComponentName);
                _adminEntity.Manager.Game.OrderHandler.HandleOrder(unassignOrder);
            }
        }

        // Assign the administrator
        post.CommanderID = _administratorId;
        post.Commander = commanderDB;
        commanderDB.AssignedTo = _adminEntity.Id;

        // From the colony/post perspective
        EventManager.Instance.Publish(
            Event.Create(
                    EventType.ColonyAdministratorAssigned,
                    atDateTime,
                    "Admin post was assigned an administrator",
                    _adminEntity.FactionOwnerID,
                    _adminEntity.Manager.ManagerID,
                    _adminEntity.Id));

        // From the administrator perspective
        EventManager.Instance.Publish(
            Event.Create(
                    EventType.AdministratorAssignedToColony,
                    atDateTime,
                    "Administrator assigned to post",
                    _adminEntity.FactionOwnerID,
                    _adminEntity.Manager.ManagerID,
                    _administratorId));
    }

    internal override bool IsFinished()
    {
        return true;
    }

    internal override bool IsValidCommand(Game game)
    {
        if (!EnableAdminRankGate)
            return true;   // byte-identical — no span-of-control gate

        // Resolve the target post + the officer the SAME way Execute does; refuse ONLY a genuine under-ranked seating.
        if (_adminEntity == null || !_adminEntity.TryGetDataBlob<AdminSpaceDB>(out var adminSpaceDB))
            return true;   // nothing to gate (no admin space) — Execute no-ops anyway

        AdminSpaceAbilityState? post = null;
        foreach (var seat in adminSpaceDB.CommanderSeats)
        {
            if (seat.ComponentName == _postComponentName)
            {
                post = seat;
                break;
            }
        }
        if (post == null)
            return true;   // unknown post → let Execute no-op on it

        if (_adminEntity.Manager == null
            || !_adminEntity.Manager.TryGetGlobalEntityById(_administratorId, out var administrator)
            || !administrator.TryGetDataBlob<CommanderDB>(out var commanderDB))
            return true;   // officer doesn't resolve → Execute no-ops

        // The one real refusal: this officer isn't senior enough for this post's AdminLevel scope.
        return AdminSpaceProcessor.CanOfficerHoldSeat(commanderDB, post.SeatType);
    }
}
