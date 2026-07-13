using System;
using Pulsar4X.Engine;
using Pulsar4X.Engine.Orders;
using Pulsar4X.Events;

namespace Pulsar4X.Sites
{
    /// <summary>
    /// Site Engine SE-5c — the player order that COMMITS a resolution branch on a field site (docs/SITE-ENGINE-DESIGN.md
    /// §4). This is what turns "study → then choose" into a real decision: a branched site accrues understanding but no
    /// longer auto-resolves (<see cref="SiteWorkProcessor"/> holds it at Worked); the player commits ONE unlocked branch
    /// with this order, which resolves the site to that branch's outcome and pays its yield.
    ///
    /// Issued to the SITE entity (which carries an <see cref="OrderableDB"/> from <see cref="FieldSiteFactory"/>), so it
    /// rides the real order rail (<c>Game.OrderHandler.HandleOrder</c>) — mirrors <see cref="Pulsar4X.GroundCombat.LoadTroopsOrder"/>.
    ///
    /// Additive: nothing issues this in the live game yet (no site carries branches; a client picker is the UI slice), so
    /// the engine is byte-identical.
    /// </summary>
    public class CommitSiteBranchOrder : EntityCommand
    {
        /// <summary>The index into <see cref="FieldSiteDB.Branches"/> the player is committing to.</summary>
        public int BranchIndex { get; private set; }

        public override ActionLaneTypes ActionLanes => ActionLaneTypes.InstantOrder;
        public override bool IsBlocking => false;
        public override string Name => "Commit Site Branch";
        public override string Details => "Resolve this site down the chosen branch";

        private Entity _entityCommanding;
        internal override Entity EntityCommanding => _entityCommanding;

        public static CommitSiteBranchOrder CreateCommand(Entity site, int branchIndex)
        {
            // A site is neutral-owned; the faction entitled to resolve it is the one that WORKED it. Attribute the order
            // to that faction (fall back to the site's owner if nobody has worked it yet).
            int requestingFaction = site.FactionOwnerID;
            if (site.TryGetDataBlob<FieldSiteDB>(out var db) && db.WorkedByFactionId >= 0)
                requestingFaction = db.WorkedByFactionId;

            return new CommitSiteBranchOrder()
            {
                _entityCommanding = site,
                EntityCommandingGuid = site.Id,
                RequestingFactionGuid = requestingFaction,
                BranchIndex = branchIndex,
            };
        }

        public override EntityCommand Clone() => throw new NotImplementedException();

        internal override bool IsFinished() => _isFinished;

        internal override void Execute(DateTime atDateTime)
        {
            var site = _entityCommanding;
            if (site?.Manager == null) { _isFinished = true; return; }
            if (!site.TryGetDataBlob<FieldSiteDB>(out var db)) { _isFinished = true; return; }

            // Terminal: the site is gone/resolved, or the choice is out of range — nothing to do, finish the order.
            if (db.YieldDelivered || BranchIndex < 0 || BranchIndex >= db.Branches.Count) { _isFinished = true; return; }

            // Try to commit the chosen branch. ResolveBranch guards it (must be Worked + that branch unlocked). If the
            // branch isn't unlocked yet (understanding still accruing), leave the order PENDING to retry — the mirror of
            // LoadTroopsOrder waiting for bay space. Once it resolves, pay the branch's yield and finish.
            if (SiteMachine.ResolveBranch(db, BranchIndex))
            {
                var branch = db.Branches[BranchIndex];
                SiteWorkProcessor.DeliverSiteYield(site, db, branch.Yield, db.Progress * branch.YieldScale);
                db.YieldDelivered = true;
                _isFinished = true;

                EventManager.Instance.Publish(Event.Create(
                    EventType.AnomalyDiscovered, atDateTime,
                    $"Site resolved: committed \"{branch.Name}\"",
                    RequestingFactionGuid, site.Manager.ManagerID, site.Id));
            }
        }

        internal override bool IsValidCommand(Game game)
        {
            if (_entityCommanding == null || !_entityCommanding.TryGetDataBlob<FieldSiteDB>(out var db)) return false;
            // A valid commit targets a real branch on a branched site that hasn't already resolved.
            return db.HasBranches && !db.YieldDelivered && BranchIndex >= 0 && BranchIndex < db.Branches.Count;
        }
    }
}
