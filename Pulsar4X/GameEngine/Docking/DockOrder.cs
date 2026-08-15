using System;
using Pulsar4X.Engine;
using Pulsar4X.Engine.Orders;

namespace Pulsar4X.Docking
{
    /// <summary>
    /// DOCK / UNDOCK — the queued ORDER wrapper over <see cref="DockTools"/>, so a player OR the AI can ask a carrier to
    /// berth or release a whole vessel through the ONE order path both seats share (the developer's law — one verb, both
    /// seats). <see cref="DockTools"/> is the capability; this is the request. Mirrors <c>Fleets.FleetOrder</c> exactly:
    /// an <see cref="EntityCommand"/> on the InstantOrder lane, commanded by the CARRIER (it owns the bay + the docked
    /// registry), targeting the ship to dock/undock.
    ///
    /// <para><b>Stock behaviour is byte-identical:</b> no base-mod ship mounts a <see cref="DockBayAtb"/>, so
    /// <c>DockTools.Capacity == 0</c> everywhere and both gates refuse; the client UI hides the entry until a hull carries
    /// a bay. Nothing schedules this on its own — it runs only when something issues it.</para>
    /// </summary>
    public class DockOrder : EntityCommand
    {
        public enum DockOrderType : byte
        {
            Dock,
            Undock,
        }

        public override ActionLaneTypes ActionLanes => ActionLaneTypes.InstantOrder;

        public override bool IsBlocking => false;

        public override string Name => "Dock Order (" + OrderType.ToString() + ")";

        public override string Details => Name;

        public DockOrderType OrderType { get; private set; }

        private Entity _carrier;     // the entity commanding — owns the DockBayAtb + the DockedShipsDB registry
        private Entity _ship;        // the vessel being docked / released

        internal override Entity EntityCommanding => _carrier;

        internal override bool IsFinished() => _isFinished;

        private DockOrder(int factionGuid, Entity carrier, Entity ship)
        {
            RequestingFactionGuid = factionGuid;
            _carrier = carrier;
            _ship = ship;
            EntityCommandingGuid = carrier.Id;
            CreatedDate = carrier.StarSysDateTime;
            UseActionLanes = true;
        }

        /// <summary>Berth <paramref name="ship"/> inside <paramref name="carrier"/> (both gates apply — door + budget).</summary>
        public static DockOrder Dock(int requestingFaction, Entity carrier, Entity ship)
            => new DockOrder(requestingFaction, carrier, ship) { OrderType = DockOrderType.Dock };

        /// <summary>Release <paramref name="ship"/> from <paramref name="carrier"/>, handing it back to the carrier's SOI.</summary>
        public static DockOrder Undock(int requestingFaction, Entity carrier, Entity ship)
            => new DockOrder(requestingFaction, carrier, ship) { OrderType = DockOrderType.Undock };

        internal override void Execute(DateTime atDateTime)
        {
            // DockTools is fully defensive (never throws) — a refusal is a readable string we drop here, because the order
            // has no return channel; the client checks CanDock and surfaces the reason BEFORE issuing (the Visibility Gate).
            switch (OrderType)
            {
                case DockOrderType.Dock:
                    DockTools.TryDock(_carrier, _ship, out _);
                    break;
                case DockOrderType.Undock:
                    DockTools.Undock(_carrier, _ship);
                    break;
            }
            _isFinished = true;
        }

        internal override bool IsValidCommand(Game game)
        {
            if (_carrier == null || _ship == null) return false;
            if (!game.Factions.ContainsKey(RequestingFactionGuid)) return false;
            // The requesting faction must own the carrier — you cannot berth into someone else's hangar. (Boarding a
            // captured hull is a separate future concern; matches FleetOrder's own-the-commanded-entity check.)
            return RequestingFactionGuid == _carrier.FactionOwnerID;
        }

        public override EntityCommand Clone()
        {
            // A real, non-throwing clone (the de-fang lesson — a throwing Clone is a latent sim-thread [FATAL] if this is
            // ever cloned as a standing action). Shallow-copies the entity refs; a fresh CmdID is fine.
            return new DockOrder(RequestingFactionGuid, _carrier, _ship)
            {
                OrderType = OrderType,
                CreatedDate = CreatedDate,
                ActionOnDate = ActionOnDate,
            };
        }
    }
}
