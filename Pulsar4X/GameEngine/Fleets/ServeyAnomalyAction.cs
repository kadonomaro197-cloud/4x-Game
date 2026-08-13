using System;
using Pulsar4X.Engine;
using Pulsar4X.Engine.Orders;

namespace Pulsar4X.Fleets;

// NOTE: the class name misspelling ("Servey") is LOAD-BEARING — TypeNameHandling.Objects embeds it in any save that
// holds this command; renaming it breaks those saves (L3). Do NOT "fix" the spelling.
public class ServeyAnomalyAction : EntityCommand
{
    public override string Name => "Survey Anomaly";
    public override string Details => "Investigate a located anomaly.";
    public override ActionLaneTypes ActionLanes { get; }
    public override bool IsBlocking { get; }

    private Entity _entityCommanding;
    internal override Entity EntityCommanding => _entityCommanding;

    // BEHAVIOR PARKED (OPERATION BLUEPRINT-TO-STEEL A4 — campaign log ADJUDICATION QUEUE): this is a near-duplicate of
    // the fully-working JPSurveyOrder (an "anomaly" is a JPSurveyableDB grav point). The decision — FINISH it as a thin
    // JPSurveyOrder clone (seed JPSurveyDB on survey-capable ships), or DELETE it and register a JPSurveyOrder-backed
    // "Survey nearest anomaly" — is the developer's; duplicating the survey logic invites drift. It is not registered
    // anywhere (unreachable) today. This slice only DE-CRASHES it: the old Execute/IsValidCommand/Clone all threw
    // NotImplementedException, which would land on the sim thread as a clock-killing [FATAL] if it were ever wired.
    internal override bool IsValidCommand(Game game) => false;   // inert until a behavior is chosen — never accepted

    internal override void Execute(DateTime atDateTime) => _isFinished = true;   // no-op safe completion (never reached while IsValidCommand is false)

    internal override bool IsFinished() => _isFinished;

    public ServeyAnomalyAction() { }
    public ServeyAnomalyAction(Entity commandingEntity) { _entityCommanding = commandingEntity; }

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
