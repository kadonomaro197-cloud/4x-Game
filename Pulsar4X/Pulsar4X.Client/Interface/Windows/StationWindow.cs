using System;
using ImGuiNET;
using Pulsar4X.Engine;
using Pulsar4X.Datablobs;
using Pulsar4X.Extensions;
using Pulsar4X.Client.Interface.Widgets;
using Pulsar4X.Stations;
using Pulsar4X.Names;

namespace Pulsar4X.Client
{
    /// <summary>
    /// The player's management window for a space STATION (Slice A — the front door's management half). A station
    /// is a parallel host to a colony but has no colony picker to reach it, so this per-entity window opens from the
    /// system-map context menu ("Manage Station") on any entity carrying a <see cref="StationInfoDB"/>.
    ///
    /// It is a THIN, defensive DRAW (the client is runtime CI-blind): a small header read straight off the station
    /// blobs, then the SAME host-agnostic <see cref="IndustryDisplay"/> the colony window uses — so a deployed
    /// platform with a constructor module lets the player queue + install further modules in-situ with no new
    /// economy code. Uses the Window.Begin/Window.End wrapper (End runs unconditionally) and wraps the body in
    /// try/catch so a throw can never skip End and cascade the whole UI (client CLAUDE.md, the 2026-07-02 lesson).
    /// </summary>
    class StationWindow : NonUniquePulsarGuiWindow
    {
        public StationWindow(EntityState entity, GlobalUIState state)
        {
            _uiState = state;
            SetName("StationWindow|" + entity.Entity.Id.ToString());
            _flags = ImGuiWindowFlags.AlwaysAutoResize;
            onEntityChange(entity);
        }

        internal void onEntityChange(EntityState entity)
        {
            _lookedAtEntity = entity;
        }

        internal static StationWindow GetInstance(EntityState entity, GlobalUIState state)
        {
            string name = "StationWindow|" + entity.Entity.Id.ToString();
            StationWindow thisItem;
            if (!_uiState.LoadedNonUniqueWindows.ContainsKey(name))
            {
                thisItem = new StationWindow(entity, state);
                thisItem.StartDisplay();
            }
            else
            {
                thisItem = (StationWindow)_uiState.LoadedNonUniqueWindows[name];
                thisItem.onEntityChange(entity);
            }

            return thisItem;
        }

        internal override void Display()
        {
            if (!IsActive || _lookedAtEntity == null) return;

            ImGui.SetNextWindowSize(new System.Numerics.Vector2(500, 500), ImGuiCond.Once);
            if (Window.Begin("Station: " + _lookedAtEntity.Name, ref IsActive, _flags))
            {
                try
                {
                    RenderHeader();
                    ImGui.Separator();
                    // The host-agnostic production display — lets the player queue/install modules on the station.
                    IndustryDisplay.GetInstance(_lookedAtEntity).Display(_uiState);
                }
                catch (Exception ex)
                {
                    ImGui.TextUnformatted("Station panel error (logged).");
                    Console.WriteLine("[RenderError] StationWindow threw: " + ex);
                }
            }
            Window.End();
        }

        private void RenderHeader()
        {
            var entity = _lookedAtEntity.Entity;
            if (!entity.TryGetDataBlob<StationInfoDB>(out var stationInfo))
            {
                ImGui.TextUnformatted("(not a station)");
                return;
            }

            int factionId = _uiState.Faction?.Id ?? -1;

            string bodyName = "unknown";
            if (stationInfo.HostingBodyEntity != null && stationInfo.HostingBodyEntity.IsValid
                && stationInfo.HostingBodyEntity.TryGetDataBlob<NameDB>(out var bodyNameDb))
                bodyName = bodyNameDb.GetName(factionId);
            ImGui.TextUnformatted("Orbiting: " + bodyName);

            // The "cheap to kill" durability pool (Slice B) — how much punishment before this station is destroyed.
            ImGui.TextUnformatted("Structural integrity: "
                + stationInfo.StructuralIntegrity.ToString("0") + " / "
                + StationInfoDB.BaseStructuralIntegrity.ToString("0"));

            long pop = 0;
            foreach (var kv in stationInfo.Population) pop += kv.Value;
            ImGui.TextUnformatted("Population: " + pop.ToString("#,##0"));

            // Operating cost (Slice C) — a live preview of the monthly upkeep drain, which climbs with the station's
            // size + function-diversity ("cheap while focused, expensive as a planet-replacement").
            decimal upkeep = StationEconomyDB.OperatingCost(entity);
            ImGui.TextUnformatted("Operating cost: " + upkeep.ToString("#,##0") + " / month");
        }
    }
}
