using System;
using System.Collections.Generic;
using Pulsar4X.Engine;
using Pulsar4X.Engine.Orders;
using Pulsar4X.Events;
using Pulsar4X.Extensions;
using Pulsar4X.Factions;
using Pulsar4X.Components;
using Pulsar4X.Datablobs;
using Pulsar4X.Storage;
using Pulsar4X.Stations;

namespace Pulsar4X.Construction
{
    /// <summary>
    /// BUILD A DESIGNED BUILDABLE ON SITE from carried components — the developer's model "A", the recipe-driven twin of
    /// <see cref="Pulsar4X.Stations.DeployStationOrder"/>. Where the deploy order conjures a bare platform for one
    /// material, this order assembles a REAL designed station out of the actual COMPONENTS you built at a factory and
    /// hauled here: it reads the station's recipe (a <see cref="StationDesign"/> — chassis + modules), checks the
    /// constructor's own hold AND its fleet-mates' holds contain every component, CONSUMES them, then creates the station
    /// at the ship's location and installs each module on it.
    ///
    /// The chain it closes (cradle-to-grave, both ends): mineral → material → COMPONENT (built at a factory) → packed in
    /// cargo → hauled by a fleet → assembled ON SITE by a constructor → a working station → destroyed (the parts are
    /// gone with it). The gate is the <see cref="ConstructorAtb"/> ability (only a unit that carries one can build), and
    /// its <see cref="ConstructorAtb.ConstructionCapacity"/> caps how big a recipe it can raise.
    ///
    /// Submitted through the real player path (<c>Game.OrderHandler.HandleOrder</c>) exactly like the deploy order —
    /// tests must use that path, not <c>Execute()</c> directly (Stations/CLAUDE.md). An <see cref="ActionLaneTypes.InstantOrder"/>
    /// with a default (due) date, so the handler runs it synchronously. FULLY GUARDED — a missing recipe / site / faction /
    /// short pool refuses cleanly (no station, an event, nothing consumed) and never throws.
    /// </summary>
    public class OnSiteConstructionOrder : EntityCommand
    {
        /// <summary>The recipe to build — a <see cref="StationDesign"/> id in the faction's <c>IndustryDesigns</c>.</summary>
        public string StationDesignId { get; private set; }

        public override ActionLaneTypes ActionLanes => ActionLaneTypes.InstantOrder;
        public override bool IsBlocking => false;
        public override string Name => "Construct Here";
        public override string Details => "Assemble a designed station on site from carried components";

        private Entity _entityCommanding;
        internal override Entity EntityCommanding => _entityCommanding;

        public static OnSiteConstructionOrder CreateCommand(Entity constructorShip, string stationDesignId)
        {
            return new OnSiteConstructionOrder()
            {
                _entityCommanding = constructorShip,
                EntityCommandingGuid = constructorShip.Id,
                RequestingFactionGuid = constructorShip.FactionOwnerID,
                StationDesignId = stationDesignId,
            };
        }

        public override EntityCommand Clone() => throw new NotImplementedException();

        internal override bool IsFinished() => _isFinished;

        internal override void Execute(DateTime atDateTime)
        {
            var ship = _entityCommanding;
            var game = ship?.Manager?.Game;
            if (game == null) { _isFinished = true; return; }

            // Where the station is born — a nearby Lagrange marker, else the body the ship is in the SOI of.
            var hostingBody = ship.GetSOIParentEntity();
            if (hostingBody == null || !hostingBody.IsValid) { _isFinished = true; return; }

            if (!game.Factions.TryGetValue(ship.FactionOwnerID, out var factionEntity)
                || !factionEntity.TryGetDataBlob<FactionInfoDB>(out var factionInfo))
            { _isFinished = true; return; }

            // Resolve the recipe.
            if (StationDesignId == null
                || !factionInfo.IndustryDesigns.TryGetValue(StationDesignId, out var recipeInfo)
                || recipeInfo is not StationDesign recipe
                || recipe.ComponentDesignIds == null || recipe.ComponentDesignIds.Count == 0)
            {
                Refuse(atDateTime, ship, "Construction aborted: no valid station recipe to build");
                return;
            }

            // The constructor ability + its capacity (the largest recipe volume it can raise).
            if (!TryGetConstructorCapacity(ship, out double capacity))
            {
                Refuse(atDateTime, ship, "Construction aborted: this ship carries no field constructor");
                return;
            }

            // Resolve each recipe component to a real ComponentDesign, and total the volume for the capacity gate.
            var parts = new List<(ComponentDesign design, int count)>();
            double totalVolume = 0;
            foreach (var kv in recipe.ComponentDesignIds)
            {
                if (kv.Value < 1) continue;
                if (!factionInfo.IndustryDesigns.TryGetValue(kv.Key, out var d) || d is not ComponentDesign part)
                {
                    Refuse(atDateTime, ship, $"Construction aborted: recipe component '{kv.Key}' is no longer a known design");
                    return;
                }
                parts.Add((part, kv.Value));
                totalVolume += part.VolumePerUnit * kv.Value;
            }

            if (totalVolume > capacity)
            {
                Refuse(atDateTime, ship,
                    $"Construction aborted: '{recipe.Name}' needs a {totalVolume:0} m³ constructor; this one is rated for {capacity:0} m³");
                return;
            }

            // Verify the whole pool holds every component BEFORE consuming anything (check-then-consume).
            var holds = ConstructionCargo.GatherPooledHolds(ship);
            foreach (var (design, count) in parts)
            {
                if (ConstructionCargo.CountPooled(holds, design) < count)
                {
                    Refuse(atDateTime, ship,
                        $"Construction aborted: not enough '{design.Name}' in the constructor or its fleet (need {count})");
                    return;
                }
            }

            // All present — consume the components from the pooled holds.
            foreach (var (design, count) in parts)
                ConstructionCargo.TryConsumePooled(holds, design, count);

            // Assemble: create the station at the site, install each module (chassis inert; functional modules light up
            // their host-agnostic economy processors on install).
            var station = StationFactory.CreateStation(factionEntity, hostingBody, recipe.InitialPopulation);
            if (station != null)
            {
                foreach (var (design, count) in parts)
                    station.AddComponent(design, count);
            }

            _isFinished = true; // the constructor survives (reusable) — one build per order

            var stationName = station != null ? station.GetName(RequestingFactionGuid) : recipe.Name;
            EventManager.Instance.Publish(
                Event.Create(
                    EventType.ColonyCreated, // reused — the signal is "a new place exists"
                    atDateTime,
                    $"{stationName} has been assembled on site from carried components",
                    RequestingFactionGuid,
                    ship.Manager.ManagerID,
                    station?.Id ?? ship.Id));
        }

        internal override bool IsValidCommand(Game game)
        {
            return _entityCommanding != null
                && TryGetConstructorCapacity(_entityCommanding, out _)
                && _entityCommanding.GetSOIParentEntity() != null;
        }

        /// <summary>Resolve the best (largest-capacity) field constructor mounted on the ship, or false if none. Reads the
        /// <see cref="ConstructorAtb"/> off each qualifying component's DESIGN. Never throws.</summary>
        private static bool TryGetConstructorCapacity(Entity ship, out double capacity)
        {
            capacity = 0;
            if (ship == null || !ship.TryGetDataBlob<ComponentInstancesDB>(out var comps)) return false;
            if (!comps.TryGetComponentsByAttribute<ConstructorAtb>(out var instances) || instances == null || instances.Count == 0)
                return false;

            bool found = false;
            foreach (var inst in instances)
            {
                var design = inst?.Design;
                if (design != null && design.HasAttribute<ConstructorAtb>())
                {
                    double c = design.GetAttribute<ConstructorAtb>().ConstructionCapacity;
                    if (c > capacity) capacity = c;
                    found = true;
                }
            }
            return found;
        }

        /// <summary>Refuse the build cleanly: resolve the order (the vessel survives), publish a visible event, consume
        /// nothing. So a failed build is legible, not a silent no-op.</summary>
        private void Refuse(DateTime atDateTime, Entity ship, string message)
        {
            _isFinished = true;
            EventManager.Instance.Publish(
                Event.Create(
                    EventType.MineralShortage, // reused — the signal is "a build couldn't be supplied"
                    atDateTime,
                    message,
                    RequestingFactionGuid,
                    ship?.Manager?.ManagerID,
                    ship?.Id));
        }
    }
}
