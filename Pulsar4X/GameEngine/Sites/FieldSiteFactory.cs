using System.Collections.Generic;
using Pulsar4X.Datablobs;
using Pulsar4X.Engine;
using Pulsar4X.Engine.Orders;
using Pulsar4X.Movement;
using Pulsar4X.Names;
using Pulsar4X.Orbital;

namespace Pulsar4X.Sites
{
    /// <summary>
    /// Site Engine SE-1b — the factory that puts a <see cref="FieldSiteDB"/> into the world as a located,
    /// point-in-space thing (docs/SITE-ENGINE-DESIGN.md, the anomaly-first build). A space anomaly is the engine
    /// minus the surface bits, so it's the cheapest first site: a NAME + a fixed POSITION (no orbit, no movement,
    /// like a jump point) + the site record. Neutral-owned — it belongs to no faction until one WORKS it.
    ///
    /// Mirrors <c>JumpPoints.JPFactory.CreateJumpPoint</c> (the other point-in-space entity): NameDB + a
    /// <see cref="PositionDB"/> with <see cref="PositionDB.MoveTypes.None"/> + a marker blob, added via
    /// <c>system.AddEntity</c> as the neutral faction. Nothing in the live New-Game path calls this yet, so the
    /// engine is byte-identical (no anomaly exists until an exploration-discovery slice spawns one).
    /// </summary>
    public static class FieldSiteFactory
    {
        /// <summary>
        /// Create a space anomaly SITE co-located with <paramref name="atPosition"/> (its absolute position is
        /// copied, so a worker parked at that point is "present"). The site's dials default to the SE-1
        /// science/research anomaly; pass overrides to author other rows.
        /// </summary>
        public static Entity CreateAnomalySite(
            StarSystem system,
            Vector3 atPosition,
            string name = "Unknown Anomaly",
            SiteRole role = SiteRole.Science,
            SiteShape shape = SiteShape.OneShot,
            SiteHook hook = SiteHook.Benign,
            SiteYield yield = SiteYield.Research,
            double understandingToResolve = 100.0)
        {
            var nameDB = new NameDB(name);

            var positionDB = new PositionDB(atPosition.X, atPosition.Y, atPosition.Z, null)
            {
                MoveType = PositionDB.MoveTypes.None // an anomaly sits still, like a jump point
            };

            var siteDB = new FieldSiteDB
            {
                Role = role,
                Shape = shape,
                Hook = hook,
                Yield = yield,
                UnderstandingToResolve = understandingToResolve
            };

            // SE-5c: an OrderableDB so the site can receive the CommitSiteBranchOrder (the player's branch choice). Inert
            // with no pending order → byte-identical (a site with no branches never issues one).
            var dataBlobs = new List<BaseDataBlob> { nameDB, positionDB, siteDB, new OrderableDB() };

            Entity site = Entity.Create();
            site.FactionOwnerID = Game.NeutralFactionId;
            system.AddEntity(site, dataBlobs);
            return site;
        }

        /// <summary>
        /// Site Engine SE-3a — create a SURFACE site located on a planet body's region/hex (docs/SITE-ENGINE-DESIGN.md
        /// §6: a ruin on the ground that ground troops must land on and work), rather than a point in space. It carries
        /// NO <see cref="PositionDB"/> — its location is the body + region + hex on the site record, and its worker is a
        /// ground unit standing on it (SE-3b), not a parked ship. Neutral-owned until a faction works it.
        ///
        /// Nothing in the live New-Game path calls this yet, and the existing space work path skips a site with no
        /// PositionDB, so a surface site is inert until SE-3b adds the ground-worker branch → byte-identical.
        /// </summary>
        public static Entity CreateSurfaceSite(
            StarSystem system,
            Entity body,
            int regionIndex,
            int globalQ,
            int globalR,
            string name = "Surface Ruin",
            SiteRole role = SiteRole.Science,
            SiteShape shape = SiteShape.OneShot,
            SiteHook hook = SiteHook.Benign,
            SiteYield yield = SiteYield.Research,
            double understandingToResolve = 100.0)
        {
            var nameDB = new NameDB(name);

            var siteDB = new FieldSiteDB
            {
                Role = role,
                Shape = shape,
                Hook = hook,
                Yield = yield,
                UnderstandingToResolve = understandingToResolve,
                SurfaceBodyEntityId = body.Id,
                SurfaceRegionIndex = regionIndex,
                SurfaceGlobalQ = globalQ,
                SurfaceGlobalR = globalR
            };

            // SE-5c: an OrderableDB so the surface site can receive the CommitSiteBranchOrder. Inert without a pending
            // order → byte-identical.
            var dataBlobs = new List<BaseDataBlob> { nameDB, siteDB, new OrderableDB() };

            Entity site = Entity.Create();
            site.FactionOwnerID = Game.NeutralFactionId;
            system.AddEntity(site, dataBlobs);
            return site;
        }
    }
}
