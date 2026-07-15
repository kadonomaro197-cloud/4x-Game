using System;
using System.Collections.Generic;
using System.Linq;
using Pulsar4X.Engine;
using Pulsar4X.Engine.Orders;
using Pulsar4X.Events;
using Pulsar4X.Extensions;
using Pulsar4X.Factions;
using Pulsar4X.Components;
using Pulsar4X.Storage;
using Pulsar4X.Movement;
using Pulsar4X.Galaxy;
using Pulsar4X.Orbital;
using Pulsar4X.Fleets;
using Pulsar4X.Ships;

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
    /// can't drop stations).
    ///
    /// MATERIALS COST (Slice F, 2026-07-03): the bare frame is no longer FREE. Deploying consumes refined materials
    /// (v1: <see cref="FrameMaterialId"/>) scaled by how the construction ship is BUILT — its cargo capacity — so a
    /// bigger constructor drops a bigger, costlier platform. The materials are drawn from the construction ship's own
    /// hold AND, if it belongs to a FLEET, from the pooled holds of its fleet-mates (send a constructor + freighters
    /// together and the whole fleet's cargo feeds the station). Not enough across the pool → the deploy is REFUSED
    /// (no station) and reported. The deployed platform's starter loadout includes a WAREHOUSE so its own hold is a
    /// real (seeded) store — a bare <c>new CargoStorageDB()</c> has no TypeStore and silently no-ops every add/remove,
    /// so without a cargo module the station couldn't receive the materials its in-situ builds need.
    /// </summary>
    public class DeployStationOrder : EntityCommand
    {
        public long InitialPopulation { get; private set; }
        public Entity Species { get; private set; }
        public List<string> StarterModuleDesignIds { get; private set; }

        /// <summary>The default bare-platform loadout: a constructor module so the platform is an in-situ builder,
        /// plus a WAREHOUSE so the platform has a real (seeded) cargo hold to receive materials for those builds.</summary>
        private static readonly List<string> DefaultStarterModules = new () { "default-design-factory", "default-design-warehouse" };

        // ---- Frame-material cost (Slice F) — PLACEHOLDER ratios, tune later. ----
        /// <summary>The refined material the bare frame is billed in (v1 bills ONE material; a multi-material bill is a refinement).</summary>
        private const string FrameMaterialId = "stainless-steel";
        /// <summary>Frame cost = this fraction of the construction ship's own cargo capacity (in the frame material's volume). PLACEHOLDER.</summary>
        private const double FrameFractionOfShipCapacity = 0.5;
        /// <summary>A floor so even a tiny constructor pays SOMETHING for a frame. PLACEHOLDER.</summary>
        private const long FrameFloorUnits = 10;

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

            // Prefer a LAGRANGE-POINT marker the ship is sitting near (a stable, named anchor) — that's the "build
            // in space at a real place, not a random point" path. Otherwise anchor to whatever body the ship is in
            // the SOI of (a star / belt / planet). Either lets a station sit where you'd never put a colony.
            var hostingBody = FindNearbyLagrangeMarker(ship) ?? ship.GetSOIParentEntity();
            if (hostingBody == null || !hostingBody.IsValid) return;

            if (!game.Factions.TryGetValue(ship.FactionOwnerID, out var factionEntity)) return;
            if (!factionEntity.TryGetDataBlob<FactionInfoDB>(out var factionInfo)) return;

            // Pay the frame's material cost from the construction ship's hold — and, if it's in a fleet, from the
            // whole fleet's pooled holds (Slice F). Not enough across the pool → refuse the deploy (no station) and
            // report it, so the failure is visible rather than a silent no-op.
            if (!TryPayFrameMaterials(ship, factionInfo))
            {
                _isFinished = true; // the order resolves (as a failed deploy); the reusable vessel survives
                EventManager.Instance.Publish(
                    Event.Create(
                        EventType.MineralShortage,
                        atDateTime,
                        $"Station deployment aborted: not enough {FrameMaterialId} in the construction ship or its fleet",
                        RequestingFactionGuid,
                        ship.Manager.ManagerID,
                        ship.Id));
                return;
            }

            var station = StationFactory.CreateStation(factionEntity, hostingBody, InitialPopulation, Species);

            // Install the starter loadout (deploy-bare-build-in-situ). Guarded: a missing design id is skipped.
            if (StarterModuleDesignIds != null)
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

        /// <summary>
        /// PLACEHOLDER capture radius (Slice D) — how close a construction ship must be to a Lagrange marker to
        /// deploy AT it. Tune with the real numbers.
        /// </summary>
        private const double LagrangeCaptureRadius_m = 5e9; // 5 Gm

        /// <summary>The nearest Lagrange-point marker within capture range of the ship, or null. Never throws.</summary>
        private static Entity FindNearbyLagrangeMarker(Entity ship)
        {
            try
            {
                if (ship?.Manager == null || !ship.TryGetDataBlob<PositionDB>(out var shipPos))
                    return null;
                Vector3 shipAbs = shipPos.AbsolutePosition;
                Entity nearest = null;
                double nearestDist = LagrangeCaptureRadius_m;
                foreach (var marker in ship.Manager.GetAllEntitiesWithDataBlob<LagrangePointDB>())
                {
                    if (!marker.TryGetDataBlob<PositionDB>(out var mp)) continue;
                    double d = (mp.AbsolutePosition - shipAbs).Length();
                    if (d <= nearestDist) { nearestDist = d; nearest = marker; }
                }
                return nearest;
            }
            catch { return null; }
        }

        /// <summary>
        /// Charge the frame's material cost to the construction ship's hold, pooling its fleet-mates' holds if it's
        /// in a fleet. Returns TRUE if the full cost was paid (materials removed), FALSE if the pool can't cover it
        /// (nothing is consumed in that case — checked before spending, so a refused deploy never half-drains a hold).
        /// </summary>
        private static bool TryPayFrameMaterials(Entity ship, FactionInfoDB factionInfo)
        {
            // Resolve the frame material in the faction's OWN goods library (null-safe string lookup — a captured or
            // foreign ship can hold ids the current faction doesn't define). If the base material is somehow
            // undefined, don't block the deploy on a data gap — treat the frame as free (defensive).
            var frameMaterial = factionInfo.Data?.CargoGoods?.GetAny(FrameMaterialId);
            if (frameMaterial == null) return true;

            long costUnits = FrameCostUnits(ship, frameMaterial);
            if (costUnits <= 0) return true;

            var holds = GatherPooledHolds(ship);

            // Check the whole pool covers the bill BEFORE removing anything (RemoveCargoByUnit clamps silently, so a
            // spend-then-discover-short path would leave a half-drained hold and no station).
            long available = 0;
            foreach (var hold in holds)
                available += hold.GetUnitsStored(frameMaterial, false);
            if (available < costUnits)
                return false;

            long remaining = costUnits;
            foreach (var hold in holds)
            {
                if (remaining <= 0) break;
                long inThisHold = hold.GetUnitsStored(frameMaterial, false);
                long take = Math.Min(inThisHold, remaining);
                if (take > 0)
                    remaining -= hold.RemoveCargoByUnit(frameMaterial, take);
            }
            return remaining <= 0;
        }

        /// <summary>The frame's cost in units of <paramref name="frameMaterial"/>: a placeholder fraction of the
        /// construction ship's OWN cargo capacity (bigger constructor → bigger, costlier platform), with a floor.</summary>
        private static long FrameCostUnits(Entity ship, ICargoable frameMaterial)
        {
            double capacityVolume = 0;
            if (ship.TryGetDataBlob<CargoStorageDB>(out var hold))
                capacityVolume = hold.GetMaxVolume(frameMaterial); // the constructor's own capacity in the frame material's cargo type
            double volumePerUnit = frameMaterial.VolumePerUnit;
            long scaled = volumePerUnit > 0
                ? (long)Math.Ceiling(capacityVolume * FrameFractionOfShipCapacity / volumePerUnit)
                : 0;
            return Math.Max(FrameFloorUnits, scaled);
        }

        /// <summary>
        /// The cargo holds the deploy can draw from: the construction ship's own hold, plus every fleet-mate's hold
        /// if the ship is in a fleet. Delegates to the shared <see cref="Pulsar4X.Construction.ConstructionCargo.GatherPooledHolds"/>
        /// so the bare-frame deploy and the recipe-driven on-site build pool holds the SAME way (no drift).
        /// </summary>
        private static List<CargoStorageDB> GatherPooledHolds(Entity ship)
            => Pulsar4X.Construction.ConstructionCargo.GatherPooledHolds(ship);
    }
}
