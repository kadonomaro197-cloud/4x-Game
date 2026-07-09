using System;
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
    /// Build a structure on a SPECIFIC HEX of a planet's surface grid — one zoom finer than
    /// <see cref="PlaceInstallationInRegionOrder"/> (which places at a whole region BAND). This is the front door for
    /// "build a mine ON that mineral deposit": deposits are located on individual <see cref="GroundHex"/>es
    /// (<see cref="GroundHex.DepositMineralId"/>), so a mine wants to sit on the exact hex that holds its ore, not
    /// vaguely "in the region". Every building being placeable at a real hex is the LOCKED PRINCIPLE at the finest
    /// grain — "everything you build on a planet is an actual building on the planet."
    ///
    /// Issued to the COLONY (which carries an <see cref="OrderableDB"/>), so it rides the real player order path
    /// (<c>Game.OrderHandler.HandleOrder</c>). It installs the component on the colony (the normal installation rail —
    /// the economy + mining processor see it exactly like any other building) AND records that instance's id in the
    /// chosen hex's <see cref="GroundHex.InstallationIds"/>. That "where" is a purely ADDITIVE located axis: the
    /// working economy is untouched, a building simply also knows which hex it sits on.
    ///
    /// v1 is DIRECT placement (like the region order) and the mine still draws from the body-wide mineral pool — the
    /// PER-HEX mining pass (a mine works the deposit on its OWN hex, and THAT hex depletes) is the flagged follow-up
    /// that promotes the located deposits to the mined source of truth. Design: docs/GLOBAL-HEX-GRID-DESIGN.md +
    /// docs/GROUND-COMBAT-MAP-DESIGN.md.
    /// </summary>
    public class PlaceInstallationOnHexOrder : EntityCommand
    {
        public int GlobalQ { get; private set; }
        public int GlobalR { get; private set; }
        public string InstallationDesignId { get; private set; }

        public override ActionLaneTypes ActionLanes => ActionLaneTypes.InstantOrder;
        public override bool IsBlocking => false;
        public override string Name => "Build on Hex";
        public override string Details => "Build an installation on a chosen surface hex of this colony's planet";

        private Entity _entityCommanding;
        internal override Entity EntityCommanding => _entityCommanding;

        public static PlaceInstallationOnHexOrder CreateCommand(Entity colony, int globalQ, int globalR, string installationDesignId)
        {
            return new PlaceInstallationOnHexOrder()
            {
                _entityCommanding = colony,
                EntityCommandingGuid = colony.Id,
                RequestingFactionGuid = colony.FactionOwnerID,
                GlobalQ = globalQ,
                GlobalR = globalR,
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

            if (!TryGetHex(colony, GlobalQ, GlobalR, out var hex)) return;

            // Install on the colony (the normal installation rail → economy + mining see it) and record WHERE it sits.
            var instance = new ComponentInstance(compDesign);
            colony.AddComponent(instance);
            hex.InstallationIds.Add(instance.ID);

            _isFinished = true;

            EventManager.Instance.Publish(
                Event.Create(
                    EventType.ProductionCompleted, // reused — the signal is "a structure now exists here"
                    atDateTime,
                    $"{compDesign.Name} built at hex ({GlobalQ},{GlobalR})",
                    RequestingFactionGuid,
                    colony.Manager.ManagerID,
                    colony.Id));
        }

        internal override bool IsValidCommand(Game game)
        {
            return _entityCommanding != null
                && _entityCommanding.HasDataBlob<ColonyInfoDB>()
                && TryGetHex(_entityCommanding, GlobalQ, GlobalR, out _);
        }

        /// <summary>Resolve the colony's planet's surface-grid hex at global (<paramref name="q"/>,<paramref name="r"/>),
        /// building the grid on demand. The column wraps at the seam (<see cref="SurfaceGrid.HexAt"/>). Never throws.</summary>
        private static bool TryGetHex(Entity colony, int q, int r, out GroundHex hex)
        {
            hex = null;
            try
            {
                if (colony == null || !colony.TryGetDataBlob<ColonyInfoDB>(out var colonyInfo)) return false;
                var planet = colonyInfo.PlanetEntity;
                if (planet == null || !planet.IsValid) return false;
                var grid = PlanetGridFactory.EnsureGridForBody(planet);
                if (grid == null) return false;
                hex = grid.HexAt(q, r);
                return hex != null;
            }
            catch { return false; }
        }
    }
}
