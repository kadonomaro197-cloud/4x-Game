using System;
using System.Collections.Generic;
using Pulsar4X.Engine;
using Pulsar4X.Engine.Orders;
using Pulsar4X.Events;
using Pulsar4X.Extensions;
using Pulsar4X.Factions;
using Pulsar4X.Components;

namespace Pulsar4X.Stations
{
    /// <summary>
    /// The player's FRONT DOOR to a station (Slice A) — the deliberate parallel to
    /// <see cref="Pulsar4X.Colonies.CreateColonyOrder"/>. It deploys a station platform at a chosen body,
    /// so the engine's <see cref="StationFactory.CreateStation"/> (which existed but only tests could call)
    /// becomes reachable through the normal order pipeline (a UI button → OrderHandler → this).
    ///
    /// Build model — "deploy bare, build in-situ" (docs/SPACE-STATIONS-DESIGN.md): the deployed platform gets a
    /// STARTER CONSTRUCTOR loadout so it can immediately fabricate + install further modules on location. The
    /// loadout is looked up defensively from the faction's own designs — a missing id is skipped, never thrown,
    /// so a faction without the constructor design just gets a truly bare platform.
    /// </summary>
    public class DeployStationOrder : EntityCommand
    {
        public Entity HostingBody { get; private set; }
        public long InitialPopulation { get; private set; }
        public Entity Species { get; private set; }
        public List<string> StarterModuleDesignIds { get; private set; }

        /// <summary>
        /// The default bare-platform loadout: a constructor module (a factory) so the platform is an in-situ
        /// builder the moment it deploys. Guarded on lookup, so this is a preference, not a hard requirement.
        /// </summary>
        private static readonly List<string> DefaultStarterModules = new () { "default-design-factory" };

        public override ActionLaneTypes ActionLanes => ActionLaneTypes.InstantOrder;

        public override bool IsBlocking => false;

        public override string Name => "Deploy Station";

        public override string Details => $"Deploy a station platform at {HostingBody?.ToString()}";

        private Entity _entityCommanding;
        internal override Entity EntityCommanding => _entityCommanding;

        public static DeployStationOrder CreateCommand(
            Entity faction,
            Entity hostingBody,
            long initialPopulation = 0,
            Entity species = null,
            List<string> starterModuleDesignIds = null)
        {
            var command = new DeployStationOrder()
            {
                _entityCommanding = faction,
                EntityCommandingGuid = faction.Id,
                RequestingFactionGuid = faction.Id,
                HostingBody = hostingBody,
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
            var station = StationFactory.CreateStation(_entityCommanding, HostingBody, InitialPopulation, Species);

            // Install the starter loadout (deploy-bare-build-in-situ). Guarded: a missing design id is skipped.
            if (_entityCommanding.TryGetDataBlob<FactionInfoDB>(out var factionInfo) && StarterModuleDesignIds != null)
            {
                foreach (var id in StarterModuleDesignIds)
                {
                    if (factionInfo.IndustryDesigns.TryGetValue(id, out var design) && design is ComponentDesign compDesign)
                        station.AddComponent(compDesign);
                }
            }

            _isFinished = true;

            var stationName = station.GetName(RequestingFactionGuid);
            EventManager.Instance.Publish(
                Event.Create(
                    EventType.ColonyCreated, // reused — no station-specific EventType yet; the signal is "a new place exists"
                    atDateTime,
                    $"{stationName} has been deployed",
                    RequestingFactionGuid,
                    _entityCommanding.Manager.ManagerID,
                    station.Id));
        }

        internal override bool IsValidCommand(Game game)
        {
            return true;
        }
    }
}
