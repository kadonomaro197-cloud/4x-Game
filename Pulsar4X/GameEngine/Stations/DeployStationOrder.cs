using System;
using System.Collections.Generic;
using Pulsar4X.Engine;
using Pulsar4X.Engine.Orders;
using Pulsar4X.Events;
using Pulsar4X.Extensions;
using Pulsar4X.Factions;
using Pulsar4X.Components;
using Pulsar4X.Storage;

namespace Pulsar4X.Stations
{
    /// <summary>
    /// Deploy a station from a CONSTRUCTION SHIP at wherever the ship is parked (Slice A2 — the ship-issued deploy
    /// path). This replaces the earlier planet-list "Deploy Station" button, which was survey-gated and body-bound —
    /// so it could NOT place a station around a star, a belt point, or an anomaly, exactly the places a station is
    /// FOR (you don't colonize a star; you orbit it with a research post). A station is now something you CARRY to a
    /// spot: fly a hauler to the star → "Deploy Station Here" → the station anchors to whatever body the ship is in
    /// the SOI of (<see cref="Pulsar4X.Extensions.EntityExtensions.GetSOIParentEntity"/>).
    ///
    /// The vessel is REUSABLE (the developer's call): it survives the deploy and can fly on to deploy again.
    /// Build model — "deploy bare, build in-situ": the deployed platform gets a starter constructor so the player
    /// can then build further modules onto it on location.
    ///
    /// v1 gate: the commanding entity is a ship with a cargo hold (a hauler/constructor vessel — a bare warship
    /// can't drop stations). LITERAL consumption of a constructor + materials from the ship's HOLD is a documented
    /// refinement tied to the materials-supply loop (StationFactory doesn't attach LogiBaseDB yet); for now the
    /// starter constructor is granted onto the new platform.
    /// </summary>
    public class DeployStationOrder : EntityCommand
    {
        public long InitialPopulation { get; private set; }
        public Entity Species { get; private set; }
        public List<string> StarterModuleDesignIds { get; private set; }

        /// <summary>The default bare-platform loadout: a constructor module so the platform is an in-situ builder.</summary>
        private static readonly List<string> DefaultStarterModules = new () { "default-design-factory" };

        public override ActionLaneTypes ActionLanes => ActionLaneTypes.InstantOrder;

        public override bool IsBlocking => false;

        public override string Name => "Deploy Station Here";

        public override string Details => "Deploy a station platform at this ship's location";

        // The construction ship issuing the order (the commanded entity).
        private Entity _entityCommanding;
        internal override Entity EntityCommanding => _entityCommanding;

        public static DeployStationOrder CreateCommand(
            Entity constructionShip,
            long initialPopulation = 0,
            Entity species = null,
            List<string> starterModuleDesignIds = null)
        {
            var command = new DeployStationOrder()
            {
                _entityCommanding = constructionShip,
                EntityCommandingGuid = constructionShip.Id,
                RequestingFactionGuid = constructionShip.FactionOwnerID,
                InitialPopulation = initialPopulation,
                Species = species,
                StarterModuleDesignIds = starterModuleDesignIds ?? DefaultStarterModules,
            };

            return command;
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
            var ship = _entityCommanding;
            var game = ship?.Manager?.Game;
            if (game == null) return;

            // The station anchors to whatever body the construction ship is in the SOI of — a star, a belt body, a
            // planet, whatever it's parked at. This is what lets a research station orbit a star you'll never colonize.
            var hostingBody = ship.GetSOIParentEntity();
            if (hostingBody == null || !hostingBody.IsValid) return;

            if (!game.Factions.TryGetValue(ship.FactionOwnerID, out var factionEntity)) return;

            var station = StationFactory.CreateStation(factionEntity, hostingBody, InitialPopulation, Species);

            // Install the starter loadout (deploy-bare-build-in-situ). Guarded: a missing design id is skipped.
            if (factionEntity.TryGetDataBlob<FactionInfoDB>(out var factionInfo) && StarterModuleDesignIds != null)
            {
                foreach (var id in StarterModuleDesignIds)
                {
                    if (factionInfo.IndustryDesigns.TryGetValue(id, out var design) && design is ComponentDesign compDesign)
                        station.AddComponent(compDesign);
                }
            }

            _isFinished = true; // the vessel survives (reusable) — the order is one deployment

            var stationName = station.GetName(RequestingFactionGuid);
            EventManager.Instance.Publish(
                Event.Create(
                    EventType.ColonyCreated, // reused — no station-specific EventType yet; the signal is "a new place exists"
                    atDateTime,
                    $"{stationName} has been deployed",
                    RequestingFactionGuid,
                    ship.Manager.ManagerID,
                    station.Id));
        }

        internal override bool IsValidCommand(Game game)
        {
            // A construction vessel = a ship with a cargo hold, parked somewhere it can anchor a station.
            return _entityCommanding != null
                && _entityCommanding.HasDataBlob<CargoStorageDB>()
                && _entityCommanding.GetSOIParentEntity() != null;
        }
    }
}
