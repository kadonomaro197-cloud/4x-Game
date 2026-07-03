using System;
using System.Collections.Generic;
using Pulsar4X.Engine;
using Pulsar4X.Engine.Orders;
using Pulsar4X.Events;
using Pulsar4X.Factions;
using Pulsar4X.Components;
using Pulsar4X.Colonies;
using Pulsar4X.Datablobs;

namespace Pulsar4X.Galaxy
{
    /// <summary>
    /// Build a structure at a SPECIFIC REGION of a planet (ground-map slice 2) — the "build at a place, not just
    /// on the colony" front door. This is the station-deploy pattern one level DOWN: the station deploy anchored a
    /// platform to an orbital body; this anchors an installation to a region of a planet's surface.
    ///
    /// Issued to the COLONY (the planetside industrial actor — colonies carry an <see cref="OrderableDB"/>, so this
    /// rides the same real player path as every other order, <c>Game.OrderHandler.HandleOrder</c>). It installs a
    /// component on the colony (the normal installation rail — the economy sees it exactly like any other building)
    /// AND records that instance's id in the chosen region's <see cref="Region.InstallationIds"/>. That "where" is a
    /// NEW located axis: the colony's existing abstract installations are left untouched, so nothing about the
    /// working economy is disturbed — a building simply now also knows which region it sits in.
    ///
    /// v1 is DIRECT placement (like the station's starter modules). Routing it through the industry queue so it
    /// consumes materials and takes build-time (a region-targeted <c>IndustryJob</c>), and building on an
    /// UNcolonised region via a ground construction unit, are documented refinements. Design:
    /// docs/GROUND-COMBAT-MAP-DESIGN.md.
    /// </summary>
    public class PlaceInstallationInRegionOrder : EntityCommand
    {
        public int RegionIndex { get; private set; }
        public string InstallationDesignId { get; private set; }

        public override ActionLaneTypes ActionLanes => ActionLaneTypes.InstantOrder;
        public override bool IsBlocking => false;
        public override string Name => "Build in Region";
        public override string Details => "Build an installation at a chosen region of this colony's planet";

        private Entity _entityCommanding;
        internal override Entity EntityCommanding => _entityCommanding;

        public static PlaceInstallationInRegionOrder CreateCommand(Entity colony, int regionIndex, string installationDesignId)
        {
            return new PlaceInstallationInRegionOrder()
            {
                _entityCommanding = colony,
                EntityCommandingGuid = colony.Id,
                RequestingFactionGuid = colony.FactionOwnerID,
                RegionIndex = regionIndex,
                InstallationDesignId = installationDesignId,
            };
        }

        public override EntityCommand Clone()
        {
            throw new NotImplementedException();
        }

        internal override bool IsFinished()
        {
            return _isFinished;
        }

        internal override void Execute(DateTime atDateTime)
        {
            var colony = _entityCommanding;
            var game = colony?.Manager?.Game;
            if (game == null) return;

            if (!game.Factions.TryGetValue(colony.FactionOwnerID, out var factionEntity)) return;
            if (!factionEntity.TryGetDataBlob<FactionInfoDB>(out var factionInfo)) return;
            if (!factionInfo.IndustryDesigns.TryGetValue(InstallationDesignId, out var design)) return;
            if (design is not ComponentDesign compDesign) return;

            if (!TryGetRegion(colony, RegionIndex, out var region)) return;

            // Install on the colony (the normal installation rail → the economy sees it) and record WHERE it sits.
            var instance = new ComponentInstance(compDesign);
            colony.AddComponent(instance);
            region.InstallationIds.Add(instance.ID);

            _isFinished = true;

            EventManager.Instance.Publish(
                Event.Create(
                    EventType.ProductionCompleted, // reused — the signal is "a structure now exists here"
                    atDateTime,
                    $"{compDesign.Name} built in region {RegionIndex}",
                    RequestingFactionGuid,
                    colony.Manager.ManagerID,
                    colony.Id));
        }

        internal override bool IsValidCommand(Game game)
        {
            return _entityCommanding != null
                && _entityCommanding.HasDataBlob<ColonyInfoDB>()
                && TryGetRegion(_entityCommanding, RegionIndex, out _);
        }

        /// <summary>Resolve the colony's planet's region at <paramref name="index"/>. Never throws.</summary>
        private static bool TryGetRegion(Entity colony, int index, out Region region)
        {
            region = null;
            try
            {
                if (colony == null || !colony.TryGetDataBlob<ColonyInfoDB>(out var colonyInfo)) return false;
                var planet = colonyInfo.PlanetEntity;
                if (planet == null || !planet.IsValid || !planet.TryGetDataBlob<PlanetRegionsDB>(out var regionsDB)) return false;
                if (index < 0 || index >= regionsDB.Regions.Count) return false;
                region = regionsDB.Regions[index];
                return region != null;
            }
            catch { return false; }
        }
    }
}
