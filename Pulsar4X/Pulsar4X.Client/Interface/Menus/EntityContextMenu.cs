using System;
using ImGuiNET;
using Pulsar4X.Engine;
using Pulsar4X.Ships;
using Pulsar4X.Storage;
using Pulsar4X.Stations;

namespace Pulsar4X.Client
{

    public class EntityContextMenu
    {
        GlobalUIState _state;
        EntityState _entityState;

        public EntityContextMenu(GlobalUIState state, int entityGuid)
        {
            _state = state;
            //_uiState.OpenWindows.Add(this);
            //IsActive = true;
            _entityState = state.StarSystemStates[state.SelectedStarSystemId].EntityStatesWithNames[entityGuid];

        }

        internal void Display()
        {
            ImGui.BeginGroup();

            void ContextButton(Type T)
            {
                //Creates a context button if it is valid
                if(EntityUIWindows.CheckIfCanOpenWindow(T, _entityState))
                {
                    if (ImGui.SmallButton(GlobalUIState.NamesForMenus[T]))
                    {
                        EntityUIWindows.OpenUIWindow(T, _entityState, _state, true ,true);
                    }
                }
            }

            //Creates all the context buttons
            ContextButton(typeof(SelectPrimaryBlankMenuHelper));
            ContextButton(typeof(PinCameraBlankMenuHelper));
            ContextButton(typeof(RenameWindow));
            ContextButton(typeof(FireControl));
            ContextButton(typeof(CargoTransferWindow));
            ContextButton(typeof(ColonyPanel));
            ContextButton(typeof(StationWindow));
            ContextButton(typeof(PlanetaryWindow));
            ContextButton(typeof(GotoSystemBlankMenuHelper));
            ContextButton(typeof(WarpOrderWindow));
            ContextButton(typeof(ChangeCurrentOrbitWindow));
            ContextButton(typeof(NavWindow));
            ContextButton(typeof(OrdersListWindow));

            // Deploy a station from a CONSTRUCTION SHIP (a hauler) at its current location — a star, belt, or
            // planet orbit. Ship-issued (not a planet-list button) so it can reach places you'd never colonize;
            // the reusable vessel survives to deploy again. Only shown for an own-faction ship with a cargo hold.
            if (_entityState.Entity.HasDataBlob<ShipInfoDB>()
                && _entityState.Entity.HasDataBlob<CargoStorageDB>()
                && _entityState.Entity.FactionOwnerID == _state.Faction?.Id
                && _state.Game != null)
            {
                if (ImGui.SmallButton("Deploy Station Here"))
                {
                    var cmd = DeployStationOrder.CreateCommand(_entityState.Entity);
                    _state.Game.OrderHandler.HandleOrder(cmd);
                    ImGui.CloseCurrentPopup();
                }
            }
            ImGui.EndGroup();

        }
    }
}
