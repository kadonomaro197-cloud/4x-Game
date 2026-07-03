using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ImGuiNET;
using Pulsar4X.Engine;
using Pulsar4X.Galaxy;
using Pulsar4X.Client.Interface.Widgets;

namespace Pulsar4X.Client
{
    /// <summary>
    /// The planet's surface, drawn as the developer's "flat 3-region view" of the 4-slice ring
    /// (<see cref="PlanetRegionsDB"/>). You see three regions at once — the CENTER one plus its two ring
    /// neighbours — and rotate around the ring with the ◀ / ▶ buttons (or by clicking a side region). Because
    /// the ring has no seam, marching "off the east edge" of the last region enters the first: the topology the
    /// Pacific-theatre requirement demanded, made visible. Each region is painted as stacked terrain bands
    /// (ocean/mountains/forest/… sized by how much of the region they cover); an UNSURVEYED region is fog until
    /// it's scanned — that's where exploration meets the map.
    ///
    /// This is a THIN, DEFENSIVE draw: every value is read straight off the engine's <see cref="PlanetRegionsDB"/>
    /// (which is CI-tested), the body is wrapped so a throw can't skip <see cref="Window.End"/>, and nothing is
    /// hard-indexed. Per-entity (keyed by the planet body id), mirroring <see cref="PlanetaryWindow"/>.
    ///
    /// Design: docs/GROUND-COMBAT-MAP-DESIGN.md (slice 3). CI compiles the client but cannot RUN it, so the live
    /// render/feel is verified on the developer's local Windows build.
    /// </summary>
    class PlanetViewWindow : NonUniquePulsarGuiWindow
    {
        // Which region sits in the middle column. Rotating changes this; the ring wraps modulo region count.
        private int _centerRegion = 0;

        // Terrain colours — a region is a bundle of these bands. Fog grey stands in for anything unknown.
        private static readonly Dictionary<RegionFeatureType, Vector4> _featureColors = new()
        {
            { RegionFeatureType.Unknown,   new Vector4(0.30f, 0.30f, 0.33f, 1f) },
            { RegionFeatureType.Ocean,     new Vector4(0.15f, 0.35f, 0.70f, 1f) },
            { RegionFeatureType.Coast,     new Vector4(0.40f, 0.60f, 0.85f, 1f) },
            { RegionFeatureType.Wetland,   new Vector4(0.30f, 0.50f, 0.45f, 1f) },
            { RegionFeatureType.Plains,    new Vector4(0.55f, 0.75f, 0.40f, 1f) },
            { RegionFeatureType.Forest,    new Vector4(0.20f, 0.50f, 0.25f, 1f) },
            { RegionFeatureType.Jungle,    new Vector4(0.10f, 0.40f, 0.15f, 1f) },
            { RegionFeatureType.Desert,    new Vector4(0.85f, 0.75f, 0.45f, 1f) },
            { RegionFeatureType.Barren,    new Vector4(0.55f, 0.50f, 0.42f, 1f) },
            { RegionFeatureType.Highlands, new Vector4(0.50f, 0.42f, 0.30f, 1f) },
            { RegionFeatureType.Mountains, new Vector4(0.45f, 0.45f, 0.50f, 1f) },
            { RegionFeatureType.Volcanic,  new Vector4(0.40f, 0.20f, 0.20f, 1f) },
            { RegionFeatureType.Tundra,    new Vector4(0.70f, 0.75f, 0.78f, 1f) },
            { RegionFeatureType.Ice,       new Vector4(0.85f, 0.90f, 0.95f, 1f) },
            { RegionFeatureType.GasLayers, new Vector4(0.80f, 0.60f, 0.40f, 1f) },
        };

        public PlanetViewWindow(EntityState entity, GlobalUIState state)
        {
            _uiState = state;
            SetName("PlanetViewWindow|" + entity.Entity.Id.ToString());
            _flags = ImGuiWindowFlags.None;
            onEntityChange(entity);
        }

        internal void onEntityChange(EntityState entity)
        {
            _lookedAtEntity = entity;
            _centerRegion = 0;
        }

        internal static PlanetViewWindow GetInstance(EntityState entity, GlobalUIState state)
        {
            string name = "PlanetViewWindow|" + entity.Entity.Id.ToString();
            PlanetViewWindow thisItem;
            if (!_uiState.LoadedNonUniqueWindows.ContainsKey(name))
            {
                thisItem = new PlanetViewWindow(entity, state);
                thisItem.StartDisplay();
            }
            else
            {
                thisItem = (PlanetViewWindow)_uiState.LoadedNonUniqueWindows[name];
                thisItem.onEntityChange(entity);
            }
            return thisItem;
        }

        internal override void Display()
        {
            if (!IsActive || _lookedAtEntity == null) return;

            ImGui.SetNextWindowSize(new Vector2(620, 460), ImGuiCond.Once);
            if (Window.Begin("Planet View: " + _lookedAtEntity.Name, ref IsActive, _flags))
            {
                try
                {
                    if (!_lookedAtEntity.Entity.TryGetDataBlob<PlanetRegionsDB>(out var regionsDB)
                        || regionsDB.Regions == null || regionsDB.Regions.Count == 0)
                    {
                        ImGui.TextWrapped("This body has no surface regions (it isn't a major body, or the region layer hasn't generated).");
                    }
                    else
                    {
                        DrawRing(regionsDB.Regions);
                    }
                }
                catch (Exception ex)
                {
                    // Never let a draw fault skip End() (CLAUDE.md: ImGui Begin/End must always balance).
                    ImGui.TextUnformatted("Planet view error (logged).");
                    Console.WriteLine($"[RenderError] PlanetViewWindow threw: {ex}");
                }
            }
            Window.End();
        }

        private void DrawRing(List<Region> regions)
        {
            int count = regions.Count;
            _centerRegion = ((_centerRegion % count) + count) % count;   // keep in range
            int left = ((_centerRegion - 1) % count + count) % count;
            int right = (_centerRegion + 1) % count;

            // ── Controls ────────────────────────────────────────────────────────────────
            if (ImGui.Button("◀ West")) _centerRegion = left;
            ImGui.SameLine();
            ImGui.Text($"Region {_centerRegion + 1} of {count}");
            ImGui.SameLine();
            if (ImGui.Button("East ▶")) _centerRegion = right;

            int surveyed = regions.Count(r => r.Surveyed);
            ImGui.SameLine();
            ImGui.TextDisabled($"   surveyed {surveyed}/{count}");
            ImGui.Separator();

            // ── The three-region strip ──────────────────────────────────────────────────
            var drawList = ImGui.GetWindowDrawList();
            var canvasPos = ImGui.GetCursorScreenPos();
            var canvasSize = ImGui.GetContentRegionAvail();
            // reserve the bottom third for the detail panel
            float mapHeight = Math.Max(120f, canvasSize.Y * 0.62f);
            var mapSize = new Vector2(canvasSize.X, mapHeight);

            ImGui.InvisibleButton("planetcanvas", mapSize);
            bool hovered = ImGui.IsItemHovered();

            float gap = 8f;
            float colW = (mapSize.X - gap * 2f) / 3f;
            int[] cols = { left, _centerRegion, right };

            for (int c = 0; c < 3; c++)
            {
                float x0 = canvasPos.X + c * (colW + gap);
                var colMin = new Vector2(x0, canvasPos.Y);
                var colMax = new Vector2(x0 + colW, canvasPos.Y + mapHeight);
                bool isCenter = (c == 1);

                DrawRegionColumn(drawList, colMin, colMax, regions[cols[c]], isCenter);

                // Click a side column to rotate it into the centre; the ring makes this seamless.
                if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    var mp = ImGui.GetIO().MousePos;
                    if (mp.X >= colMin.X && mp.X <= colMax.X && mp.Y >= colMin.Y && mp.Y <= colMax.Y)
                        _centerRegion = cols[c];
                }
            }

            // advance the cursor past the canvas, then draw the detail of the centre region
            ImGui.Separator();
            DrawRegionDetail(regions[_centerRegion]);
        }

        private void DrawRegionColumn(ImDrawListPtr drawList, Vector2 min, Vector2 max, Region region, bool isCenter)
        {
            float h = max.Y - min.Y;
            uint borderCol = ImGui.ColorConvertFloat4ToU32(isCenter
                ? new Vector4(1f, 1f, 1f, 1f)
                : new Vector4(0.5f, 0.5f, 0.5f, 1f));

            if (!region.Surveyed)
            {
                // Fog: unknown until scanned.
                uint fog = ImGui.ColorConvertFloat4ToU32(_featureColors[RegionFeatureType.Unknown]);
                drawList.AddRectFilled(min, max, fog);
                DrawCentredText(drawList, min, max, "?  UNSURVEYED", new Vector4(0.75f, 0.75f, 0.78f, 1f));
                DrawBorder(drawList, min, max, borderCol, isCenter ? 2.5f : 1f);
                return;
            }

            // Stacked terrain bands, sized by coverage (normalised so they fill the column).
            var feats = region.Features != null && region.Features.Count > 0
                ? region.Features
                : new List<RegionFeature> { new RegionFeature(RegionFeatureType.Barren, 1.0) };
            double total = feats.Sum(f => Math.Max(0.0001, f.Coverage));

            float y = min.Y;
            foreach (var f in feats)
            {
                float bandH = (float)(Math.Max(0.0001, f.Coverage) / total) * h;
                var bMin = new Vector2(min.X, y);
                var bMax = new Vector2(max.X, Math.Min(max.Y, y + bandH));
                var col = _featureColors.TryGetValue(f.Type, out var vc) ? vc : _featureColors[RegionFeatureType.Barren];
                drawList.AddRectFilled(bMin, bMax, ImGui.ColorConvertFloat4ToU32(col));

                if (bandH > 16f)
                {
                    string label = $"{f.Type} {f.Coverage:P0}";
                    // dark or light text depending on band brightness, for legibility
                    float lum = 0.299f * col.X + 0.587f * col.Y + 0.114f * col.Z;
                    var txtCol = lum > 0.6f ? new Vector4(0.1f, 0.1f, 0.1f, 1f) : new Vector4(1f, 1f, 1f, 1f);
                    drawList.AddText(new Vector2(bMin.X + 4f, bMin.Y + 2f),
                        ImGui.ColorConvertFloat4ToU32(txtCol), label);
                }
                y += bandH;
            }

            // Region label + border
            drawList.AddText(new Vector2(min.X + 4f, max.Y - 18f),
                ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 1f, 1f, 1f)),
                $"Region {region.Index + 1}");
            if (region.InstallationIds != null && region.InstallationIds.Count > 0)
            {
                drawList.AddText(new Vector2(min.X + 4f, max.Y - 34f),
                    ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 0.9f, 0.5f, 1f)),
                    $"⚙ {region.InstallationIds.Count}");
            }
            DrawBorder(drawList, min, max, borderCol, isCenter ? 2.5f : 1f);
        }

        private static void DrawBorder(ImDrawListPtr drawList, Vector2 min, Vector2 max, uint color, float thickness)
        {
            var tr = new Vector2(max.X, min.Y);
            var bl = new Vector2(min.X, max.Y);
            drawList.AddLine(min, tr, color, thickness);
            drawList.AddLine(tr, max, color, thickness);
            drawList.AddLine(max, bl, color, thickness);
            drawList.AddLine(bl, min, color, thickness);
        }

        private static void DrawCentredText(ImDrawListPtr drawList, Vector2 min, Vector2 max, string text, Vector4 color)
        {
            var size = ImGui.CalcTextSize(text);
            var pos = new Vector2((min.X + max.X) * 0.5f - size.X * 0.5f, (min.Y + max.Y) * 0.5f - size.Y * 0.5f);
            drawList.AddText(pos, ImGui.ColorConvertFloat4ToU32(color), text);
        }

        private void DrawRegionDetail(Region region)
        {
            ImGui.Text($"Selected: Region {region.Index + 1}");
            if (!region.Surveyed)
            {
                ImGui.TextDisabled("Unsurveyed — scan this world to reveal its geography.");
                return;
            }
            ImGui.Text($"Area: {region.Area_km2:#,##0} km²");
            ImGui.SameLine();
            ImGui.Text($"   Crossing time: {FormatTime(region.CrossingTimeSeconds)}");
            ImGui.SameLine();
            ImGui.Text($"   Installations: {region.InstallationIds?.Count ?? 0}");

            if (region.Features != null && region.Features.Count > 0)
            {
                ImGui.TextDisabled("Terrain:");
                ImGui.SameLine();
                ImGui.TextUnformatted(string.Join(", ",
                    region.Features.OrderByDescending(f => f.Coverage)
                                   .Select(f => $"{f.Type} {f.Coverage:P0}")));
            }
        }

        private static string FormatTime(double seconds)
        {
            if (seconds <= 0) return "—";
            var ts = TimeSpan.FromSeconds(seconds);
            if (ts.TotalDays >= 1) return $"{ts.TotalDays:0.0} d";
            if (ts.TotalHours >= 1) return $"{ts.TotalHours:0.0} h";
            return $"{ts.TotalMinutes:0} m";
        }
    }
}
