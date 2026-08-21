using System;
using Pulsar4X.Engine;
using Pulsar4X.Engine.Orders;
using Pulsar4X.Events;
using Pulsar4X.Factions;
using Pulsar4X.Colonies;
using Pulsar4X.Datablobs;
using Pulsar4X.Storage;

namespace Pulsar4X.Galaxy
{
    /// <summary>
    /// HAUL goods between two surface hexes of a body — or from a hex to the colony's cargo hold (H1, the "hex-to-hex
    /// hauling" the resource-locality ruling demands: once a mine on a hex fills that hex's local <see cref="GroundHex.Stockpile"/>
    /// (R1b, flag-gated), the ore SITS there until something carries it, so hauling becomes a real job).
    ///
    /// This is the ONE verb both seats issue: an <see cref="EntityCommand"/> on the COLONY (which carries an
    /// <see cref="OrderableDB"/>), so it rides the real order path (<c>Game.OrderHandler.HandleOrder</c>) exactly the way
    /// the AI issues an order — never a player-only panel with a crude AI shortcut behind it (the One-Verb-Both-Seats law).
    ///
    /// v1 is a DIRECT, conserved move (an ordered logistics haul): it moves what's on hand, capped at the destination's
    /// room, and removes from the source only what the destination accepted (no ore is lost or created). The
    /// design's fuller fidelity — a losable HAULER UNIT that physically marches source→dest over real distance (so a
    /// decapitated convoy strands its load) — is the cradle-to-grave follow-up (it needs a per-unit mineral-cargo model
    /// ground units don't have yet). Inert until per-hex mining fills a bucket. Design: docs/ground/UNITS-ON-THE-MAP-DESIGN.md §3.
    /// </summary>
    public class HexHaulOrder : EntityCommand
    {
        public int SrcQ { get; private set; }
        public int SrcR { get; private set; }
        public int DstQ { get; private set; }
        public int DstR { get; private set; }
        /// <summary>true = haul from the source hex to the COLONY's cargo hold (bring ore home); false = hex → dest hex.</summary>
        public bool ToColonyCargo { get; private set; }
        /// <summary>The good to move, as an <c>ICargoable.ID</c> (the same int the hex stockpile is keyed by).</summary>
        public int CargoableId { get; private set; }
        /// <summary>Units to move; ≤ 0 = move ALL of that good on hand at the source.</summary>
        public long Amount { get; private set; }

        public override ActionLaneTypes ActionLanes => ActionLaneTypes.InstantOrder;
        public override bool IsBlocking => false;
        public override string Name => "Haul goods";
        public override string Details => ToColonyCargo
            ? $"Haul goods from hex ({SrcQ},{SrcR}) to the colony's stores"
            : $"Haul goods from hex ({SrcQ},{SrcR}) to hex ({DstQ},{DstR})";

        private Entity _entityCommanding;
        internal override Entity EntityCommanding => _entityCommanding;

        /// <summary>Haul <paramref name="amount"/> (≤0 = all) of <paramref name="cargoableId"/> from a hex to a hex on the same body.</summary>
        public static HexHaulOrder CreateHexToHex(Entity colony, int srcQ, int srcR, int dstQ, int dstR, int cargoableId, long amount)
        {
            return new HexHaulOrder()
            {
                _entityCommanding = colony,
                EntityCommandingGuid = colony.Id,
                RequestingFactionGuid = colony.FactionOwnerID,
                SrcQ = srcQ, SrcR = srcR, DstQ = dstQ, DstR = dstR,
                ToColonyCargo = false,
                CargoableId = cargoableId, Amount = amount,
            };
        }

        /// <summary>Haul <paramref name="amount"/> (≤0 = all) of <paramref name="cargoableId"/> from a hex into the colony's cargo hold.</summary>
        public static HexHaulOrder CreateHexToColony(Entity colony, int srcQ, int srcR, int cargoableId, long amount)
        {
            return new HexHaulOrder()
            {
                _entityCommanding = colony,
                EntityCommandingGuid = colony.Id,
                RequestingFactionGuid = colony.FactionOwnerID,
                SrcQ = srcQ, SrcR = srcR,
                ToColonyCargo = true,
                CargoableId = cargoableId, Amount = amount,
            };
        }

        public override EntityCommand Clone()
        {
            // Real, non-throwing clone (the ServeyAnomaly/A4 lesson — a throwing Clone is a sim-thread [FATAL] if the
            // order is ever used as a standing action).
            return new HexHaulOrder()
            {
                _entityCommanding = _entityCommanding,
                EntityCommandingGuid = EntityCommandingGuid,
                RequestingFactionGuid = RequestingFactionGuid,
                SrcQ = SrcQ, SrcR = SrcR, DstQ = DstQ, DstR = DstR,
                ToColonyCargo = ToColonyCargo,
                CargoableId = CargoableId, Amount = Amount,
            };
        }

        internal override bool IsFinished() => _isFinished;

        internal override void Execute(DateTime atDateTime)
        {
            var colony = _entityCommanding;
            var game = colony?.Manager?.Game;
            if (game == null) { _isFinished = true; return; }

            if (!TryGetHex(colony, SrcQ, SrcR, out var src) || src == null) { _isFinished = true; return; }

            long onHand = src.StockpileOf(CargoableId);
            long want = Amount <= 0 ? onHand : Math.Min(Amount, onHand);
            if (want <= 0) { _isFinished = true; return; }   // nothing to carry

            long moved;
            if (ToColonyCargo)
            {
                // Bring ore home: add to the colony's cargo hold (capped by free volume), and remove from the hex ONLY
                // what the hold accepted — so a full hold strands the remainder on the hex rather than deleting it.
                if (!colony.TryGetDataBlob<CargoStorageDB>(out var stockpile)) { _isFinished = true; return; }
                if (!colony.GetFactionOwner.TryGetDataBlob<FactionInfoDB>(out var factionInfo)) { _isFinished = true; return; }
                var lib = factionInfo.Data.CargoGoods;
                ICargoable cargoable = lib.Contains(CargoableId) ? lib.GetAny(CargoableId)
                                     : (factionInfo.Data.LockedCargoGoods.Contains(CargoableId) ? factionInfo.Data.LockedCargoGoods.GetAny(CargoableId) : null);
                if (cargoable == null || !stockpile.TypeStores.ContainsKey(cargoable.CargoTypeID)) { _isFinished = true; return; }
                long added = stockpile.AddCargoByUnit(cargoable, want);
                moved = src.RemoveFromStockpile(CargoableId, added);
            }
            else
            {
                // Hex → hex on the same body. Hex buckets are unbounded, so everything on hand moves.
                if (!TryGetHex(colony, DstQ, DstR, out var dst) || dst == null) { _isFinished = true; return; }
                moved = HaulBetweenHexes(src, dst, CargoableId, want);
            }

            _isFinished = true;

            if (moved > 0)
                EventManager.Instance.Publish(
                    Event.Create(
                        EventType.ProductionCompleted, // reused — "goods moved on the surface"
                        atDateTime,
                        ToColonyCargo
                            ? $"Hauled {moved} to the colony stores from hex ({SrcQ},{SrcR})"
                            : $"Hauled {moved} from hex ({SrcQ},{SrcR}) to ({DstQ},{DstR})",
                        RequestingFactionGuid,
                        colony.Manager.ManagerID,
                        colony.Id));
        }

        internal override bool IsValidCommand(Game game)
        {
            return _entityCommanding != null
                && _entityCommanding.HasDataBlob<ColonyInfoDB>()
                && TryGetHex(_entityCommanding, SrcQ, SrcR, out _)
                && (ToColonyCargo || TryGetHex(_entityCommanding, DstQ, DstR, out _));
        }

        /// <summary>Move up to <paramref name="amount"/> (≤0 = all) of <paramref name="cargoableId"/> from
        /// <paramref name="src"/> to <paramref name="dst"/>, conserved (dst gains exactly what src loses). Returns the
        /// amount moved. Pure + testable. No-op on a null hex or an empty source.</summary>
        internal static long HaulBetweenHexes(GroundHex src, GroundHex dst, int cargoableId, long amount)
        {
            if (src == null || dst == null) return 0;
            long onHand = src.StockpileOf(cargoableId);
            long want = amount <= 0 ? onHand : Math.Min(amount, onHand);
            long taken = src.RemoveFromStockpile(cargoableId, want);
            dst.AddToStockpile(cargoableId, taken);
            return taken;
        }

        /// <summary>Resolve the colony's planet's surface-grid hex at global (q,r), building the grid on demand. Never throws.</summary>
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
