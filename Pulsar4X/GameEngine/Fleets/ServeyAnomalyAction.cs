using System;
using Pulsar4X.Engine;
using Pulsar4X.Engine.Orders;
using Pulsar4X.Extensions;     // HasJPSurveyAbililty, GetDistanceTo_m
using Pulsar4X.JumpPoints;     // JPSurveyableDB, JPSurveyOrder, IsSurveyComplete (JPSurveyableDBExtensions)
using Pulsar4X.Movement;       // WarpFleetTowardsTargetOrder, PositionDB

namespace Pulsar4X.Fleets;

// NOTE: the class name misspelling ("Servey") is LOAD-BEARING — TypeNameHandling.Objects embeds it in any save that
// holds this command; renaming it breaks those saves (L3). Do NOT "fix" the spelling.
//
// OPERATION BLUEPRINT-TO-STEEL B6 (2026-08-17): this stub now has real BEHAVIOR — "survey the nearest anomaly."
// An "anomaly" IS a JPSurveyableDB (a gravitational survey point); the fully-working survey machinery already exists
// (JPSurveyOrder + JPSurveyProcessor), so this order does NOT duplicate it — it finds the NEAREST un-surveyed anomaly
// in the fleet's own system and dispatches the SAME two orders the client's JP-survey menu issues: a fleet warp to the
// anomaly, then a JPSurveyOrder to survey it. It is the combined "go survey the nearest anomaly" convenience neither
// existing order gives on its own (MoveToNearestAnomalyAction only MOVES; JPSurveyOrder only surveys a picked target),
// and it is the same primitive a player and the AI can both issue (one verb, both seats). Save-safe: the target is
// resolved lazily at Execute time (nothing entity-referencing is stored, so there is no serialized-target gap).
public class ServeyAnomalyAction : EntityCommand
{
    public override string Name => "Survey Anomaly";
    public override string Details => "Move to and survey the nearest un-surveyed grav anomaly in this system.";
    public override ActionLaneTypes ActionLanes => ActionLaneTypes.Movement | ActionLaneTypes.InteractWithExternalEntity;
    public override bool IsBlocking => true;

    private Entity _entityCommanding;
    internal override Entity EntityCommanding => _entityCommanding;

    internal override bool IsFinished() => _isFinished;

    internal override void Execute(DateTime atDateTime)
    {
        if (IsRunning) return;   // dispatch once — the issued warp+survey orders carry the work from here
        IsRunning = true;

        // Only a survey-capable fleet can actually survey; a null/incapable fleet is a safe no-op completion.
        if (_entityCommanding == null || !_entityCommanding.HasJPSurveyAbililty())
        {
            _isFinished = true;
            return;
        }

        var target = FindNearestUnsurveyedAnomaly();
        if (target != null)
        {
            var game = _entityCommanding.Manager.Game;
            // The exact two-order pattern the client's JP-survey menu uses (FleetWindow): warp there, then survey it.
            game.OrderHandler.HandleOrder(WarpFleetTowardsTargetOrder.CreateCommand(_entityCommanding, target));
            game.OrderHandler.HandleOrder(JPSurveyOrder.CreateCommand(RequestingFactionGuid, _entityCommanding, target));
        }
        // Dispatcher: complete so this leaves the order lane; the warp+survey it issued occupy the lane until done, so a
        // STANDING "survey nearest anomaly" order does not re-fire (and pick the next anomaly) until they finish.
        _isFinished = true;
    }

    /// <summary>The nearest un-surveyed grav anomaly (a <see cref="JPSurveyableDB"/>-bearing entity) to the fleet's
    /// flagship, in the fleet's OWN system, or null if there is none / the fleet can't be resolved. Internal so the
    /// gauge can assert the core selection logic directly.</summary>
    internal Entity FindNearestUnsurveyedAnomaly()
    {
        if (_entityCommanding == null || _entityCommanding.Manager == null) return null;
        if (!_entityCommanding.TryGetDataBlob<FleetDB>(out var fleetDB)) return null;

        PositionDB flagshipPos = null;
        if (fleetDB.FlagShipID != -1 && _entityCommanding.Manager.TryGetEntityById(fleetDB.FlagShipID, out var flagship))
            flagship.TryGetDataBlob<PositionDB>(out flagshipPos);

        Entity nearest = null;
        double best = double.MaxValue;
        foreach (var db in _entityCommanding.Manager.GetAllDataBlobsOfType<JPSurveyableDB>())
        {
            if (db.IsSurveyComplete(RequestingFactionGuid)) continue;      // per-faction un-surveyed only
            var anomaly = db.OwningEntity;
            if (anomaly == null || !anomaly.TryGetDataBlob<PositionDB>(out var pos)) continue;
            double dist = flagshipPos == null ? 0 : pos.GetDistanceTo_m(flagshipPos);
            if (dist < best) { best = dist; nearest = anomaly; }
        }
        return nearest;
    }

    internal override bool IsValidCommand(Game game)
    {
        // Resolve the commanding fleet (verifies the requesting faction owns it) and require it to be survey-capable.
        // Does NOT require a live anomaly right now — a standing order should persist until one is worth surveying,
        // and Execute is a safe no-op when there is nothing un-surveyed to reach.
        if (!CommandHelpers.IsCommandValid(game.GlobalManager, RequestingFactionGuid, EntityCommandingGuid, out _, out _entityCommanding))
            return false;
        return _entityCommanding.HasJPSurveyAbililty();
    }

    public ServeyAnomalyAction() { }
    public ServeyAnomalyAction(Entity commandingEntity) { _entityCommanding = commandingEntity; }

    public static ServeyAnomalyAction CreateCommand(int factionId, Entity fleet)
    {
        return new ServeyAnomalyAction(fleet)
        {
            UseActionLanes = true,
            RequestingFactionGuid = factionId,
            EntityCommandingGuid = fleet.Id
        };
    }

    public override EntityCommand Clone() => new ServeyAnomalyAction(EntityCommanding)
    {
        UseActionLanes = this.UseActionLanes,
        RequestingFactionGuid = this.RequestingFactionGuid,
        EntityCommandingGuid = this.EntityCommandingGuid,
        CreatedDate = this.CreatedDate,
        ActionOnDate = this.ActionOnDate,
        ActionedOnDate = this.ActionedOnDate,
        IsRunning = this.IsRunning
    };
}
