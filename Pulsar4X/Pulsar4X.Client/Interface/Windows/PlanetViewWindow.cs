using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ImGuiNET;
using Pulsar4X.Engine;
using Pulsar4X.Galaxy;
using Pulsar4X.GroundCombat;
using Pulsar4X.Colonies;
using Pulsar4X.Components;
using Pulsar4X.DataStructures;
using Pulsar4X.Client.Interface.Widgets;

namespace Pulsar4X.Client
{
    /// <summary>
    /// The planet's surface as a NAVIGABLE TACTICAL MAP (ground-map slice 5e) — the upgrade from the read-only
    /// slice-3 view into "put your hands on the ground war." Still the developer's "flat 3-region view" of the
    /// 4-slice ring (<see cref="PlanetRegionsDB"/>): the CENTRE region plus its two ring neighbours, rotated with
    /// ◀ / ▶ or by clicking a region (the ring has no seam, so the Pacific survives). On top of the terrain it now
    /// draws the things you FIGHT with and OVER:
    ///
    ///   • **Units** — every <see cref="GroundUnit"/> in <see cref="GroundForcesDB"/>, grouped per region by faction
    ///     and type into a token ("I ×3" + a health bar), coloured by owner (cyan = yours, red = hostile). Click a
    ///     token to SELECT that group; with a group selected, click an adjacent region (or a March button) to order
    ///     the march (<see cref="GroundForces.OrderMove"/> — one ring-hop, validated engine-side).
    ///   • **Hazards** — the region's <see cref="PlanetEnvironmentsDB"/> environments as coloured chips (fire = red,
    ///     corrosive = green, storm/jam = amber) so you can read where the ground itself is deadly.
    ///   • **Terrain class** — Open / Cover / Rough (<see cref="GroundTerrain.Classify"/>), the cover/affinity dial.
    ///   • **Buildings** — the LOCKED PRINCIPLE ("everything I build that's selectable in space is a real building on
    ///     the ground"): each <see cref="Region.InstallationIds"/> entry is a ⚙ marker, and a Build panel places an
    ///     installation at the centre region via <see cref="PlaceInstallationInRegionOrder"/> on the real order path.
    ///
    /// THIN + DEFENSIVE by the client discipline: every value is read off CI-tested engine blobs, orders go through
    /// the CI-tested <see cref="GroundForces.OrderMove"/> / <c>Game.OrderHandler.HandleOrder</c> paths (no new client
    /// logic), the whole body is wrapped so a throw logs <c>[RenderError]</c> once and still runs <see cref="Window.End"/>,
    /// and nothing is hard-indexed. Per-entity (keyed by the planet body id). Design: docs/GROUND-COMBAT-MAP-DESIGN.md
    /// (slice 5e). **CI compiles the client but cannot RUN it — the live render/feel is the developer's local build.**
    /// </summary>
    class PlanetViewWindow : NonUniquePulsarGuiWindow
    {
        // Which region sits in the middle column. Rotating changes this; the ring wraps modulo region count.
        private int _centerRegion = 0;

        // The selected unit GROUP (region, owner faction, type) — the thing a March order acts on. -1 region = none.
        private int _selRegion = -1;
        private int _selFaction = -1;
        private GroundUnitType _selType = GroundUnitType.Infantry;

        // The installation the Build panel will place (index into the faction's placeable designs, rebuilt each frame).
        private int _buildChoice = 0;

        // The selected FORMATION (its FormationId) for the formation command panel; -1 = none.
        private int _selFormationId = -1;
        // The stance-combo index for the selected formation's stance selector.
        private int _stanceChoice = 0;

        // A transient status line ("marched 3 units east", "built Barracks in region 2") shown under the controls.
        private string _status = "";

        // ── HEX ZOOM (U3): -1 = the region-ring view; >=0 = that region's Region.Hexes drawn as a hex grid.
        //    A source hex (with your units) is selected, then a target hex is clicked to march (H2 hex pathfinder). ──
        private int _hexZoomRegion = -1;   // legacy single-region zoom (superseded by the 3-region hex map, V3) — kept for the old helpers, always -1
        private bool _hexSelActive = false;             // a source hex (with your units) is selected, awaiting a target-hex click
        private int _selHexRegion = -1, _selHexQ, _selHexR;   // the selected source hex (region-aware for cross-region march, V3)
        private float _hexSize = 0f;                    // hex draw size in px (0 = auto-fit; mouse wheel zooms) — V3

        // Token hit-rects gathered during the draw, resolved against the click AFTER the columns are drawn.
        private readonly List<(Vector2 min, Vector2 max, int region, int faction, GroundUnitType type)> _tokenHits = new();

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
            ClearSelection();
        }

        private void ClearSelection() { _selRegion = -1; _selFaction = -1; }
        private bool HasSelection => _selRegion >= 0;

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

            ImGui.SetNextWindowSize(new Vector2(680, 560), ImGuiCond.Once);
            if (Window.Begin("Planet View: " + _lookedAtEntity.Name, ref IsActive, _flags))
            {
                try
                {
                    // ONE window, THREE tabs (the developer's unified planet view): the SURFACE map (regions → hexes),
                    // your GROUND forces (formations + stance), and the COLONY (planet data + population + build).
                    if (!_lookedAtEntity.Entity.TryGetDataBlob<PlanetRegionsDB>(out var regionsDB)
                        || regionsDB.Regions == null || regionsDB.Regions.Count == 0)
                    {
                        ImGui.TextWrapped("This body has no surface regions (it isn't a major body, or the region layer hasn't generated).");
                    }
                    else if (ImGui.BeginTabBar("planetview_tabs"))
                    {
                        var regions = regionsDB.Regions;
                        SafeTab("Surface map",   () => DrawTacticalMap(regions));
                        SafeTab("Ground forces", () => DrawGroundForcesTab(regions));
                        SafeTab("Colony",        () => DrawColonyTab(regionsDB));
                        ImGui.EndTabBar();
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

        /// <summary>Render one tab body, guaranteeing <c>EndTabItem</c> always runs so a tab throw can't leave the
        /// tab bar / window stack unbalanced (the "a throwing tab cascaded the whole UI" gotcha — Client CLAUDE.md).</summary>
        private void SafeTab(string label, Action body)
        {
            if (ImGui.BeginTabItem(label))
            {
                try { body(); }
                catch (Exception ex)
                {
                    ImGui.TextUnformatted(label + " tab error (logged).");
                    Console.WriteLine($"[RenderError] PlanetViewWindow/{label} threw: {ex}");
                }
                finally { ImGui.EndTabItem(); }
            }
        }

        // ── The map ──────────────────────────────────────────────────────────────────────
        private void DrawTacticalMap(List<Region> regions)
        {
            int count = regions.Count;
            _centerRegion = ((_centerRegion % count) + count) % count;   // keep in range

            int left = ((_centerRegion - 1) % count + count) % count;
            int right = (_centerRegion + 1) % count;

            var body = _lookedAtEntity.Entity;
            int myFaction = _uiState.Faction?.Id ?? -1;
            body.TryGetDataBlob<GroundForcesDB>(out var forcesDB);
            body.TryGetDataBlob<PlanetEnvironmentsDB>(out var envDB);

            // ── Controls (cycle regions) ──
            if (ImGui.Button("◀ West")) { _centerRegion = left; _hexSelActive = false; }
            ImGui.SameLine();
            ImGui.Text($"Region {_centerRegion + 1} of {count}");
            ImGui.SameLine();
            if (ImGui.Button("East ▶")) { _centerRegion = right; _hexSelActive = false; }
            int surveyed = regions.Count(r => r.Surveyed);
            ImGui.SameLine();
            ImGui.TextDisabled($"  surveyed {surveyed}/{count}  ·  scroll to zoom · click a side region to recentre");
            if (!string.IsNullOrEmpty(_status))
            {
                ImGui.SameLine();
                ImGui.TextColored(new Vector4(0.6f, 0.9f, 0.6f, 1f), "  " + _status);
            }
            ImGui.Separator();

            // ── The 3-region HEX map (V3 default): centre region + its two ring neighbours drawn as hex patches;
            //    scroll to zoom, click a hex with your units then a target hex to march (H2 pathfinder, cross-region). ──
            DrawThreeRegionHexMap(regions, forcesDB, myFaction, left, _centerRegion, right);

            // ── Below the canvas: selection/march actions + region detail (build → Colony tab,
            //    formations → Ground forces tab; the map stays "see + move on the surface"). ─────────
            ImGui.Separator();
            DrawSelectionBar(regions, forcesDB, myFaction, left, right);
            ImGui.Separator();
            DrawRegionDetail(regions[_centerRegion], forcesDB, envDB);
            DrawLegend();
        }

        // ── The 3-region HEX map (V3 default) — centre region + its two ring neighbours drawn side-by-side as hex
        //    patches (terrain-coloured, units on hexes). Scroll to zoom; click a hex with your units then a target hex
        //    (in ANY of the three) to march via the H2 pathfinder (cross-region); click empty space in a side region to
        //    recentre. This is the "colony hex map" as the default surface, on the save-safe Region.Hexes layer. ──
        private void DrawThreeRegionHexMap(List<Region> regions, GroundForcesDB forcesDB, int myFaction, int left, int center, int right)
        {
            var drawList = ImGui.GetWindowDrawList();
            var canvasPos = ImGui.GetCursorScreenPos();
            var canvasSize = ImGui.GetContentRegionAvail();
            float availH = Math.Max(180f, canvasSize.Y * 0.62f);

            ImGui.InvisibleButton("planethexcanvas", new Vector2(canvasSize.X, availH));
            bool hovered = ImGui.IsItemHovered();
            bool clicked = hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
            var mp = ImGui.GetIO().MousePos;

            int[] slots = { left, center, right };
            const float gap = 10f;
            float slotW = (canvasSize.X - gap * 2f) / 3f;

            // Patch radius (max hex distance from centre) across the visible regions.
            int radius = 0;
            foreach (var si in slots)
            {
                var hx = regions[si].Hexes;
                if (hx == null) continue;
                foreach (var h in hx) { int d = (Math.Abs(h.Q) + Math.Abs(h.R) + Math.Abs(h.Q + h.R)) / 2; if (d > radius) radius = d; }
            }
            if (radius <= 0)
            {
                ImGui.SetCursorScreenPos(new Vector2(canvasPos.X, canvasPos.Y + 8f));
                ImGui.TextWrapped("No hex grid for these regions yet — colonise the world or complete a geological survey to generate its hexes.");
                return;
            }

            // Auto-fit one region's disk to a slot, then apply the wheel-zoom.
            float fit = Math.Min(slotW / (3f * radius + 3f), availH / (1.7320508f * (2 * radius + 1) + 2f));
            if (_hexSize <= 0f) _hexSize = fit;
            if (hovered)
            {
                float wheel = ImGui.GetIO().MouseWheel;
                if (wheel != 0f) _hexSize = Math.Clamp(_hexSize + wheel * Math.Max(0.5f, _hexSize * 0.15f), fit * 0.5f, fit * 6f);
            }
            float size = Math.Clamp(_hexSize, 2f, 60f);

            // Draw each visible region as a hex disk centred in its slot.
            for (int c = 0; c < 3; c++)
            {
                int ri = slots[c];
                var region = regions[ri];
                var origin = new Vector2(canvasPos.X + c * (slotW + gap) + slotW * 0.5f, canvasPos.Y + availH * 0.5f);

                if (region.Hexes != null)
                    foreach (var h in region.Hexes)
                    {
                        var col = _featureColors.TryGetValue(h.Terrain, out var fc) ? fc : new Vector4(0.30f, 0.30f, 0.32f, 1f);
                        DrawHexFilled(drawList, HexCenter(h.Q, h.R, origin, size), size * 0.94f, ImGui.ColorConvertFloat4ToU32(col));
                    }

                if (forcesDB != null)
                {
                    var perHex = new Dictionary<(int, int, int), int>();
                    foreach (var u in forcesDB.Units)
                    {
                        if (u.RegionIndex != ri) continue;
                        var key = (u.HexQ, u.HexR, u.FactionOwnerID);
                        perHex[key] = perHex.TryGetValue(key, out var nn) ? nn + 1 : 1;
                    }
                    foreach (var kv in perHex)
                    {
                        var cc = HexCenter(kv.Key.Item1, kv.Key.Item2, origin, size);
                        drawList.AddCircleFilled(cc, size * 0.42f, ImGui.ColorConvertFloat4ToU32(OwnerColor(kv.Key.Item3, myFaction)));
                        if (size > 12f) { string lbl = kv.Value.ToString(); var ts = ImGui.CalcTextSize(lbl); drawList.AddText(cc - ts * 0.5f, 0xFF000000, lbl); }
                    }
                }

                if (_hexSelActive && _selHexRegion == ri)
                    DrawHexOutline(drawList, HexCenter(_selHexQ, _selHexR, origin, size), size * 0.94f, 0xFFFFFFFF, 2.5f);

                string hdr = "R" + (ri + 1) + (c == 1 ? " (centre)" : "");
                drawList.AddText(new Vector2(canvasPos.X + c * (slotW + gap) + 4f, canvasPos.Y + 2f), 0xFFCFCFCF, hdr);
            }

            // Click: pick a hex (in whichever slot), then select-your-units / march / recentre.
            if (clicked)
            {
                int slot = (int)((mp.X - canvasPos.X) / (slotW + gap));
                if (slot >= 0 && slot < 3)
                {
                    int ri = slots[slot];
                    var region = regions[ri];
                    var origin = new Vector2(canvasPos.X + slot * (slotW + gap) + slotW * 0.5f, canvasPos.Y + availH * 0.5f);
                    GroundHex hit = null; float best = size;
                    if (region.Hexes != null)
                        foreach (var h in region.Hexes)
                        {
                            float dist = Vector2.Distance(mp, HexCenter(h.Q, h.R, origin, size));
                            if (dist <= size * 0.96f && dist < best) { best = dist; hit = h; }
                        }
                    if (hit != null)
                    {
                        if (!_hexSelActive)
                        {
                            if (AnyOwnUnitOnHex(forcesDB, ri, hit.Q, hit.R, myFaction))
                            { _hexSelActive = true; _selHexRegion = ri; _selHexQ = hit.Q; _selHexR = hit.R; _status = ""; }
                        }
                        else if (_selHexRegion == ri && hit.Q == _selHexQ && hit.R == _selHexR)
                        {
                            _hexSelActive = false;   // click the source again to deselect
                        }
                        else
                        {
                            int moved = MarchOwnUnitsHex(_lookedAtEntity.Entity, forcesDB, _selHexRegion, _selHexQ, _selHexR, ri, hit.Q, hit.R, myFaction);
                            _status = moved > 0 ? $"marched {moved} → R{ri + 1}({hit.Q},{hit.R})" : "no route";
                            _hexSelActive = false;
                        }
                    }
                    else if (slot != 1)
                    {
                        _centerRegion = ri; _hexSelActive = false;   // empty space in a side region → recentre
                    }
                }
            }
        }

        // ── Ground forces tab: organise units into formations + set their stance (the "manage your army"
        //    view; you SEE and MOVE them on the Surface map tab). ─────────────────────────────────────
        private void DrawGroundForcesTab(List<Region> regions)
        {
            var body = _lookedAtEntity.Entity;
            int myFaction = _uiState.Faction?.Id ?? -1;
            body.TryGetDataBlob<GroundForcesDB>(out var forcesDB);

            int count = regions.Count;
            _centerRegion = ((_centerRegion % count) + count) % count;
            int left = ((_centerRegion - 1) % count + count) % count;
            int right = (_centerRegion + 1) % count;

            if (forcesDB == null || forcesDB.Units.Count == 0)
            {
                ImGui.TextWrapped("No ground forces on this world yet. Raise units (DevTools → Raise Ground Unit) or build them, then form them up here.");
                return;
            }

            ImGui.TextDisabled("Form up idle units in the current region into a formation, then march/stance them as one. Move on the Surface map tab.");
            ImGui.Separator();
            DrawFormationPanel(body, forcesDB, myFaction, regions, left, right);
        }

        // ── Colony tab: the planet + colony readout and the build-a-base placement (the old PlanetaryWindow,
        //    folded into the one unified window). Thin, guarded reads off the same blobs. ────────────────
        private void DrawColonyTab(PlanetRegionsDB regionsDB)
        {
            var body = _lookedAtEntity.Entity;
            int myFaction = _uiState.Faction?.Id ?? -1;

            // Planet summary — reads off the body (present on any major body).
            if (body.TryGetDataBlob<SystemBodyInfoDB>(out var sb))
                ImGui.Text("Type: " + sb.BodyType.ToString());
            if (body.TryGetDataBlob<MassVolumeDB>(out var mv))
                ImGui.Text($"Radius: {mv.RadiusInM / 1000.0:0} km");
            if (body.TryGetDataBlob<AtmosphereDB>(out var atmo))
            {
                string water = atmo.Hydrosphere ? atmo.HydrosphereExtent.ToString() + "% water" : "no hydrosphere";
                // TextUnformatted: the water string can contain a literal '%', which ImGui.Text would parse as a
                // printf format specifier (Client CLAUDE.md printf trap).
                ImGui.TextUnformatted($"Surface temp: {atmo.SurfaceTemperature:0.0} °C  ({water})");
            }

            // Your colony on this world, if any.
            var colony = FindOwnColony(body, myFaction);
            if (colony != null && colony.TryGetDataBlob<ColonyInfoDB>(out var ci))
            {
                long pop = 0;
                foreach (var kv in ci.Population) pop += kv.Value;
                ImGui.Text($"Population: {pop:N0}");
            }
            else
            {
                ImGui.TextDisabled("No colony of yours on this world.");
            }

            // Surface buildings = the located installations drawn on the map (Region.InstallationIds).
            int buildings = 0;
            foreach (var r in regionsDB.Regions) buildings += r.InstallationIds?.Count ?? 0;
            ImGui.Text($"Surface buildings: {buildings}");

            ImGui.Separator();
            // Place a base at the current region (the LOCKED-principle placement — a real building on the ground).
            DrawBuildPanel(body, myFaction);
        }

        // ── HEX ZOOM (U3): the region's real Region.Hexes drawn as a hex grid — terrain-coloured hexagons with your
        //    units on their hexes, and click-a-hex-to-march (the H2 hex pathfinder made playable). This is the "colony
        //    hex map" reborn on the SAVE-SAFE ground layer (the old non-persistent ColonyHexMapDB/Window is retired). ──
        private void DrawHexZoom(List<Region> regions)
        {
            int ri = _hexZoomRegion;
            var body = _lookedAtEntity.Entity;
            int myFaction = _uiState.Faction?.Id ?? -1;
            body.TryGetDataBlob<GroundForcesDB>(out var forcesDB);

            if (ImGui.Button("◀ Back to regions")) { _hexZoomRegion = -1; _hexSelActive = false; return; }
            ImGui.SameLine();
            ImGui.Text($"Region {ri + 1} — hex view");
            if (_hexSelActive)
            {
                ImGui.SameLine();
                ImGui.TextDisabled($"  hex ({_selHexQ},{_selHexR}) selected — click a target hex to march");
            }
            if (!string.IsNullOrEmpty(_status))
            {
                ImGui.SameLine();
                ImGui.TextColored(new Vector4(0.6f, 0.9f, 0.6f, 1f), "  " + _status);
            }
            ImGui.Separator();
            DrawHexGrid(ri, regions, forcesDB, myFaction);
        }

        private void DrawHexGrid(int ri, List<Region> regions, GroundForcesDB forcesDB, int myFaction)
        {
            var region = regions[ri];
            var hexes = region.Hexes;
            if (hexes == null || hexes.Count == 0)
            {
                ImGui.TextWrapped("No hex grid for this world yet — colonise it or complete a geological survey to generate its hexes.");
                return;
            }

            // Fit the whole disk to the available canvas (scale the hex size to the patch radius).
            int radius = 0;
            foreach (var h in hexes)
            {
                int d = (Math.Abs(h.Q) + Math.Abs(h.R) + Math.Abs(h.Q + h.R)) / 2;
                if (d > radius) radius = d;
            }
            var drawList = ImGui.GetWindowDrawList();
            var canvasPos = ImGui.GetCursorScreenPos();
            var canvasSize = ImGui.GetContentRegionAvail();
            float availH = Math.Max(160f, canvasSize.Y - 4f);
            float size = Math.Min(canvasSize.X / (3f * radius + 3f), availH / (1.7320508f * (2 * radius + 1) + 2f));
            size = Math.Clamp(size, 3f, 44f);
            var origin = canvasPos + new Vector2(canvasSize.X * 0.5f, availH * 0.5f);

            ImGui.InvisibleButton("hexcanvas", new Vector2(canvasSize.X, availH));
            bool clicked = ImGui.IsItemHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
            var mp = ImGui.GetIO().MousePos;

            // 1) terrain hexes (colour by feature, reusing the ring view's palette).
            foreach (var h in hexes)
            {
                var c = HexCenter(h.Q, h.R, origin, size);
                var col = _featureColors.TryGetValue(h.Terrain, out var fc) ? fc : new Vector4(0.30f, 0.30f, 0.32f, 1f);
                DrawHexFilled(drawList, c, size * 0.94f, ImGui.ColorConvertFloat4ToU32(col));
            }

            // 2) units on their hexes (a dot + count per faction per hex; colour by owner).
            if (forcesDB != null)
            {
                var perHex = new Dictionary<(int, int, int), int>();   // q, r, faction -> standing-unit count
                foreach (var u in forcesDB.Units)
                {
                    if (u.RegionIndex != ri) continue;
                    var key = (u.HexQ, u.HexR, u.FactionOwnerID);
                    perHex[key] = perHex.TryGetValue(key, out var n) ? n + 1 : 1;
                }
                foreach (var kv in perHex)
                {
                    var c = HexCenter(kv.Key.Item1, kv.Key.Item2, origin, size);
                    drawList.AddCircleFilled(c, size * 0.42f, ImGui.ColorConvertFloat4ToU32(OwnerColor(kv.Key.Item3, myFaction)));
                    if (size > 12f)
                    {
                        string lbl = kv.Value.ToString();
                        var ts = ImGui.CalcTextSize(lbl);
                        drawList.AddText(c - ts * 0.5f, 0xFF000000, lbl);
                    }
                }
            }

            // 3) selected source-hex outline.
            if (_hexSelActive)
                DrawHexOutline(drawList, HexCenter(_selHexQ, _selHexR, origin, size), size * 0.94f, 0xFFFFFFFF, 2.5f);

            // 4) click resolution: pick your units on a hex, then click a target hex to march them (H2 pathfinder).
            if (clicked)
            {
                GroundHex hit = null;
                float best = size;
                foreach (var h in hexes)
                {
                    float dist = Vector2.Distance(mp, HexCenter(h.Q, h.R, origin, size));
                    if (dist <= size * 0.96f && dist < best) { best = dist; hit = h; }
                }
                if (hit != null)
                {
                    if (!_hexSelActive)
                    {
                        if (AnyOwnUnitOnHex(forcesDB, ri, hit.Q, hit.R, myFaction))
                        { _hexSelActive = true; _selHexQ = hit.Q; _selHexR = hit.R; _status = ""; }
                    }
                    else if (hit.Q == _selHexQ && hit.R == _selHexR)
                    {
                        _hexSelActive = false;   // click the source again to deselect
                    }
                    else
                    {
                        int moved = MarchOwnUnitsHex(_lookedAtEntity.Entity, forcesDB, ri, _selHexQ, _selHexR, ri, hit.Q, hit.R, myFaction);
                        _status = moved > 0 ? $"marched {moved} to ({hit.Q},{hit.R})" : "no route";
                        _hexSelActive = false;
                    }
                }
            }
        }

        private static bool AnyOwnUnitOnHex(GroundForcesDB forcesDB, int ri, int q, int r, int myFaction)
        {
            if (forcesDB == null) return false;
            foreach (var u in forcesDB.Units)
                if (u.FactionOwnerID == myFaction && u.RegionIndex == ri && u.HexQ == q && u.HexR == r) return true;
            return false;
        }

        /// <summary>March every standing OWN unit on the source hex to the target hex via the H2 hex pathfinder
        /// (<see cref="GroundForces.OrderMove(Entity, GroundUnit, int, int, int)"/>). Returns how many got a route.</summary>
        private int MarchOwnUnitsHex(Entity body, GroundForcesDB forcesDB, int fromRegion, int fromQ, int fromR, int toRegion, int toQ, int toR, int myFaction)
        {
            if (forcesDB == null) return 0;
            int moved = 0;
            foreach (var u in forcesDB.Units.ToArray())
            {
                if (u.FactionOwnerID != myFaction || u.RegionIndex != fromRegion) continue;
                if (u.HexQ != fromQ || u.HexR != fromR) continue;
                if (u.MovingToRegion >= 0 || (u.Path != null && u.Path.Count > 0)) continue;
                if (GroundForces.OrderMove(body, u, toRegion, toQ, toR)) moved++;   // H2 pathfinder crosses region borders
            }
            return moved;
        }

        // Flat-top axial hex layout + hexagon geometry (same math the retired ColonyHexMapWindow used, fed by Region.Hexes).
        private static Vector2 HexCenter(int q, int r, Vector2 origin, float size)
        {
            float x = size * (1.5f * q);
            float y = size * (0.8660254f * q + 1.7320508f * r);
            return origin + new Vector2(x, y);
        }

        private static void DrawHexFilled(ImDrawListPtr dl, Vector2 c, float size, uint col)
        {
            var p = new Vector2[6];
            for (int i = 0; i < 6; i++) { double a = i * Math.PI / 3.0; p[i] = new Vector2(c.X + size * (float)Math.Cos(a), c.Y + size * (float)Math.Sin(a)); }
            dl.AddConvexPolyFilled(ref p[0], 6, col);
        }

        private static void DrawHexOutline(ImDrawListPtr dl, Vector2 c, float size, uint col, float th)
        {
            var p = new Vector2[6];
            for (int i = 0; i < 6; i++) { double a = i * Math.PI / 3.0; p[i] = new Vector2(c.X + size * (float)Math.Cos(a), c.Y + size * (float)Math.Sin(a)); }
            for (int i = 0; i < 6; i++) dl.AddLine(p[i], p[(i + 1) % 6], col, th);
        }

        private void DrawRegionColumn(ImDrawListPtr drawList, Vector2 min, Vector2 max, Region region,
            bool isCenter, int myFaction, GroundForcesDB forcesDB, PlanetEnvironmentsDB envDB)
        {
            float h = max.Y - min.Y;
            uint borderCol = ImGui.ColorConvertFloat4ToU32(isCenter
                ? new Vector4(1f, 1f, 1f, 1f)
                : new Vector4(0.5f, 0.5f, 0.5f, 1f));

            if (!region.Surveyed)
            {
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
                    float lum = 0.299f * col.X + 0.587f * col.Y + 0.114f * col.Z;
                    var txtCol = lum > 0.6f ? new Vector4(0.1f, 0.1f, 0.1f, 1f) : new Vector4(1f, 1f, 1f, 1f);
                    drawList.AddText(new Vector2(bMin.X + 4f, bMin.Y + 2f),
                        ImGui.ColorConvertFloat4ToU32(txtCol), label);
                }
                y += bandH;
            }

            // Terrain class chip (top-right) — the cover/affinity read.
            var tclass = GroundTerrain.Classify(region);
            string tstr = tclass.ToString().ToUpperInvariant();
            var tsz = ImGui.CalcTextSize(tstr);
            drawList.AddText(new Vector2(max.X - tsz.X - 6f, min.Y + 3f),
                ImGui.ColorConvertFloat4ToU32(new Vector4(0.95f, 0.95f, 0.8f, 1f)), tstr);

            // Hazard chips (top-left, stacked) — where the ground itself is deadly.
            if (envDB != null)
            {
                float hy = min.Y + 3f;
                foreach (var env in envDB.ForRegion(region.Index))
                {
                    var hc = HazardColor(env.Effect);
                    var chipMin = new Vector2(min.X + 4f, hy);
                    var txt = env.Name ?? env.Effect.ToString();
                    var csz = ImGui.CalcTextSize(txt);
                    var chipMax = new Vector2(chipMin.X + csz.X + 8f, hy + csz.Y + 2f);
                    if (chipMax.Y > max.Y - 70f) break;   // don't run into the unit strip
                    drawList.AddRectFilled(chipMin, chipMax, ImGui.ColorConvertFloat4ToU32(hc), 3f);
                    drawList.AddText(new Vector2(chipMin.X + 4f, hy + 1f),
                        ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 1f, 1f, 1f)), txt);
                    hy = chipMax.Y + 2f;
                }
            }

            // Ownership + installations line, just above the unit strip.
            string owner = OwnerLabel(region.OwnerFactionID, myFaction);
            drawList.AddText(new Vector2(min.X + 4f, max.Y - 52f),
                ImGui.ColorConvertFloat4ToU32(OwnerColor(region.OwnerFactionID, myFaction)),
                $"Held: {owner}");
            if (region.InstallationIds != null && region.InstallationIds.Count > 0)
                drawList.AddText(new Vector2(min.X + 4f, max.Y - 36f),
                    ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 0.9f, 0.5f, 1f)),
                    $"⚙ {region.InstallationIds.Count} building(s)");

            // ── Unit tokens (grouped by faction+type) laid across the bottom strip ──────
            DrawUnitTokens(drawList, min, max, region, myFaction, forcesDB);

            // Region label + border
            drawList.AddText(new Vector2(min.X + 4f, max.Y - 18f),
                ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 1f, 1f, 1f)),
                $"Region {region.Index + 1}");
            DrawBorder(drawList, min, max, borderCol, isCenter ? 2.5f : 1f);
        }

        private void DrawUnitTokens(ImDrawListPtr drawList, Vector2 min, Vector2 max, Region region,
            int myFaction, GroundForcesDB forcesDB)
        {
            if (forcesDB == null) return;

            // Group this region's units by (faction, type): count, total health, total max.
            var groups = new Dictionary<(int fac, GroundUnitType t), (int n, double hp, double max, int moving)>();
            foreach (var u in forcesDB.Units)
            {
                if (u.RegionIndex != region.Index) continue;
                var key = (u.FactionOwnerID, u.UnitType);
                groups.TryGetValue(key, out var g);
                g.n += 1; g.hp += u.Health; g.max += u.MaxHealth;
                if (u.MovingToRegion >= 0) g.moving += 1;
                groups[key] = g;
            }
            if (groups.Count == 0) return;

            float tokW = 52f, tokH = 30f, tgap = 5f;
            float x = min.X + 4f;
            float ty = max.Y - 88f;
            foreach (var kv in groups)
            {
                if (x + tokW > max.X - 2f) { x = min.X + 4f; ty += tokH + tgap; }   // wrap
                if (ty + tokH > max.Y - 20f) break;                                  // out of room

                var tMin = new Vector2(x, ty);
                var tMax = new Vector2(x + tokW, ty + tokH);
                var facCol = OwnerColor(kv.Key.fac, myFaction);
                drawList.AddRectFilled(tMin, tMax, ImGui.ColorConvertFloat4ToU32(facCol), 3f);

                bool isSel = HasSelection && _selRegion == region.Index && _selFaction == kv.Key.fac && _selType == kv.Key.t;
                uint tb = ImGui.ColorConvertFloat4ToU32(isSel
                    ? new Vector4(1f, 1f, 0.3f, 1f) : new Vector4(0f, 0f, 0f, 0.7f));
                drawList.AddRect(tMin, tMax, tb, 3f, ImDrawFlags.None, isSel ? 2.5f : 1f);

                string tag = $"{TypeInitial(kv.Key.t)} x{kv.Value.n}" + (kv.Value.moving > 0 ? "»" : "");
                drawList.AddText(new Vector2(x + 4f, ty + 2f),
                    ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 1f, 1f, 1f)), tag);

                // Health bar along the bottom of the token.
                double frac = kv.Value.max > 0 ? Math.Clamp(kv.Value.hp / kv.Value.max, 0, 1) : 0;
                var hbMin = new Vector2(x + 2f, ty + tokH - 6f);
                var hbMax = new Vector2(x + tokW - 2f, ty + tokH - 2f);
                drawList.AddRectFilled(hbMin, hbMax, ImGui.ColorConvertFloat4ToU32(new Vector4(0.15f, 0.15f, 0.15f, 1f)));
                var hbFill = new Vector2(hbMin.X + (float)((hbMax.X - hbMin.X) * frac), hbMax.Y);
                drawList.AddRectFilled(hbMin, hbFill, ImGui.ColorConvertFloat4ToU32(HealthColor((float)frac)));

                _tokenHits.Add((tMin, tMax, region.Index, kv.Key.fac, kv.Key.t));
                x += tokW + tgap;
            }
        }

        // ── Selection action bar (March buttons) ────────────────────────────────────────
        private void DrawSelectionBar(List<Region> regions, GroundForcesDB forcesDB, int myFaction, int left, int right)
        {
            if (!HasSelection) { ImGui.TextDisabled("Click a unit token to select a group, then March it to an adjacent region."); return; }

            bool mine = _selFaction == myFaction;
            int n = forcesDB?.Units.Count(u => u.RegionIndex == _selRegion && u.FactionOwnerID == _selFaction && u.UnitType == _selType) ?? 0;
            ImGui.TextColored(OwnerColor(_selFaction, myFaction),
                $"Selected: {n}× {_selType} — {OwnerLabel(_selFaction, myFaction)} in Region {_selRegion + 1}");

            if (!mine) { ImGui.SameLine(); ImGui.TextDisabled("(not yours — can't order)"); return; }

            // March to whichever visible neighbours are actually adjacent to the selected group.
            var sel = _selRegion < regions.Count ? regions[_selRegion] : null;
            if (sel != null)
            {
                if (sel.Neighbors.Contains(left))
                {
                    if (ImGui.Button($"◀ March to Region {left + 1}")) MarchSelectedTo(regions, left);
                    ImGui.SameLine();
                }
                if (sel.Neighbors.Contains(right))
                {
                    if (ImGui.Button($"March to Region {right + 1} ▶")) MarchSelectedTo(regions, right);
                    ImGui.SameLine();
                }
            }
            if (ImGui.Button("Deselect")) ClearSelection();
        }

        /// <summary>March every orderable unit in the selected group to <paramref name="target"/> (one ring-hop).</summary>
        private void MarchSelectedTo(List<Region> regions, int target)
        {
            var body = _lookedAtEntity.Entity;
            if (!body.TryGetDataBlob<GroundForcesDB>(out var forcesDB)) return;
            int myFaction = _uiState.Faction?.Id ?? -1;
            if (_selFaction != myFaction) { _status = "those aren't your units"; return; }

            int moved = 0;
            // Snapshot to array: OrderMove mutates unit fields; the roster list itself isn't resized here.
            foreach (var u in forcesDB.Units.ToArray())
            {
                if (u.RegionIndex != _selRegion || u.FactionOwnerID != _selFaction || u.UnitType != _selType) continue;
                if (u.MovingToRegion >= 0) continue;   // already marching
                if (GroundForces.OrderMove(body, u, target)) moved++;
            }
            _status = moved > 0 ? $"marched {moved}× {_selType} to Region {target + 1}" : "no units could march (adjacency/transit)";
        }

        // ── Build panel — the LOCKED PRINCIPLE: place a real building on the ground ──────
        private void DrawBuildPanel(Entity body, int myFaction)
        {
            var colony = FindOwnColony(body, myFaction);
            if (colony == null)
            {
                ImGui.TextDisabled("Build: no colony of yours on this world (buildings are placed from a colony).");
                return;
            }

            var designs = PlaceableInstallations(myFaction);
            if (designs.Count == 0)
            {
                ImGui.TextDisabled("Build: no installation designs available yet (research/design one first).");
                return;
            }

            _buildChoice = Math.Clamp(_buildChoice, 0, designs.Count - 1);
            ImGui.Text("Build in Region " + (_centerRegion + 1) + ":");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(220f);
            if (ImGui.BeginCombo("##buildchoice", designs[_buildChoice].Name))
            {
                for (int i = 0; i < designs.Count; i++)
                {
                    bool sel = i == _buildChoice;
                    if (ImGui.Selectable(designs[i].Name, sel)) _buildChoice = i;
                    if (sel) ImGui.SetItemDefaultFocus();
                }
                ImGui.EndCombo();
            }
            ImGui.SameLine();
            if (ImGui.Button("Build here"))
            {
                try
                {
                    var order = PlaceInstallationInRegionOrder.CreateCommand(colony, _centerRegion, designs[_buildChoice].UniqueID);
                    _uiState.Game.OrderHandler.HandleOrder(order);
                    _status = $"queued {designs[_buildChoice].Name} in Region {_centerRegion + 1}";
                }
                catch (Exception ex)
                {
                    _status = "build order failed (logged)";
                    Console.WriteLine($"[RenderError] PlanetViewWindow build order threw: {ex}");
                }
            }
        }

        /// <summary>The player's colony sitting on THIS body, if any (a build order is issued to a colony).</summary>
        private Entity FindOwnColony(Entity body, int myFaction)
        {
            try
            {
                if (body.Manager == null) return null;
                foreach (var e in body.Manager.GetAllEntitiesWithDataBlob<ColonyInfoDB>())
                {
                    if (e.FactionOwnerID != myFaction) continue;
                    if (e.TryGetDataBlob<ColonyInfoDB>(out var ci) && ci.PlanetEntity == body) return e;
                }
            }
            catch { }
            return null;
        }

        /// <summary>The installation (planet-buildable) ComponentDesigns the viewed faction can place.</summary>
        private List<ComponentDesign> PlaceableInstallations(int myFaction)
        {
            var list = new List<ComponentDesign>();
            try
            {
                if (_uiState.Faction == null || !_uiState.Faction.TryGetDataBlob<Pulsar4X.Factions.FactionInfoDB>(out var fi))
                    return list;
                foreach (var kv in fi.IndustryDesigns)
                {
                    if (kv.Value is ComponentDesign cd
                        && cd.ComponentMountType.HasFlag(ComponentMountType.PlanetInstallation))
                        list.Add(cd);
                }
                list.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            }
            catch { }
            return list;
        }

        // ── Formations — command a group of units as one (the fleet echo) ───────────────
        private void DrawFormationPanel(Entity body, GroundForcesDB forcesDB, int myFaction, List<Region> regions, int left, int right)
        {
            if (forcesDB == null) return;
            ImGui.Separator();
            ImGui.TextDisabled("Formations (move a whole group as one — the ground echo of a fleet):");

            // Create a formation from all YOUR units standing in the centre region.
            int ownHere = forcesDB.Units.Count(u => u.FactionOwnerID == myFaction && u.RegionIndex == _centerRegion && u.MovingToRegion < 0);
            if (ownHere > 0)
            {
                if (ImGui.Button($"Form up {ownHere} unit(s) in Region {_centerRegion + 1}"))
                {
                    try
                    {
                        var f = GroundForces.CreateFormation(body, myFaction, "");
                        foreach (var u in forcesDB.Units.Where(u => u.FactionOwnerID == myFaction && u.RegionIndex == _centerRegion && u.MovingToRegion < 0).ToArray())
                            GroundForces.AssignUnit(f, u);
                        _selFormationId = f.FormationId;
                        _status = $"formed '{f.Name}' — {ownHere} unit(s)";
                    }
                    catch (Exception ex) { _status = "form-up failed (logged)"; Console.WriteLine($"[RenderError] PlanetViewWindow form-up threw: {ex}"); }
                }
            }
            else
            {
                ImGui.TextDisabled($"(No idle units of yours in Region {_centerRegion + 1} to form up.)");
            }

            // List your formations; select one to command it.
            var mine = GroundFormationTools.FormationsFor(forcesDB, myFaction);
            if (mine.Count == 0) return;

            foreach (var f in mine)
            {
                int count = GroundFormationTools.MemberCount(forcesDB, f);
                int rally = GroundForces.LeaderRegion(forcesDB, f);
                bool sel = f.FormationId == _selFormationId;
                string label = $"{f.Name} — {count} unit(s)" + (rally >= 0 ? $" @ Region {rally + 1}" : " (empty)") + $"##form{f.FormationId}";
                if (ImGui.Selectable(label, sel))
                {
                    _selFormationId = f.FormationId;
                    if (rally >= 0) _centerRegion = rally;   // navigate to the formation
                }

                if (sel)
                {
                    // March the whole formation to a visible adjacent region (of its rally region), or disband.
                    var rallyRegion = (rally >= 0 && rally < regions.Count) ? regions[rally] : null;
                    if (rallyRegion != null && count > 0)
                    {
                        if (rallyRegion.Neighbors.Contains(left))
                        {
                            if (ImGui.Button($"◀ March formation to Region {left + 1}##fm{f.FormationId}")) MarchFormation(body, f, left);
                            ImGui.SameLine();
                        }
                        if (rallyRegion.Neighbors.Contains(right))
                        {
                            if (ImGui.Button($"March formation to Region {right + 1} ▶##fm{f.FormationId}")) MarchFormation(body, f, right);
                            ImGui.SameLine();
                        }
                    }
                    if (ImGui.Button($"Disband##fm{f.FormationId}"))
                    {
                        try { GroundForces.DisbandFormation(forcesDB, f); _selFormationId = -1; _status = "formation disbanded"; }
                        catch (Exception ex) { Console.WriteLine($"[RenderError] PlanetViewWindow disband threw: {ex}"); }
                        break;   // list mutated — stop iterating this frame
                    }

                    DrawStanceSelector(f);
                }
            }
        }

        // The formation STANCE selector (the ground echo of the Fleet-window doctrine selector).
        private void DrawStanceSelector(GroundFormation formation)
        {
            if (_uiState.Game == null) return;
            var catalog = _uiState.Game.StartingGameData.GroundStances;
            if (catalog == null || catalog.Count == 0) return;

            var stances = catalog.Values.ToArray();
            var names = stances.Select(st => $"{st.DisplayName} [{st.Family}]").ToArray();
            _stanceChoice = Math.Clamp(_stanceChoice, 0, stances.Length - 1);

            string current = string.IsNullOrEmpty(formation.StanceId) ? "Balanced (none)" : $"{formation.StanceId} [{formation.StanceFamily}]";
            ImGui.TextDisabled($"Stance: {current}  (atk ×{formation.AttackMult:0.00}, dmg-taken ×{formation.DamageTakenMult:0.00})");

            ImGui.SetNextItemWidth(200f);
            ImGui.Combo($"##stance{formation.FormationId}", ref _stanceChoice, names, names.Length);

            var body = _lookedAtEntity.Entity;
            bool haveTime = body.Manager != null;
            DateTime now = haveTime ? body.StarSysDateTime : DateTime.MinValue;
            bool onCooldown = haveTime && now < formation.SwitchableAfter;

            ImGui.SameLine();
            if (!haveTime || onCooldown) ImGui.BeginDisabled();
            if (ImGui.Button($"Set stance##st{formation.FormationId}"))
            {
                try
                {
                    var bp = stances[_stanceChoice];
                    bool ok = GroundFormationDoctrine.TrySetStance(formation, bp, now);
                    _status = ok ? $"stance: {bp.DisplayName}" : "on cooldown — can't switch yet";
                }
                catch (Exception ex) { _status = "set stance failed (logged)"; Console.WriteLine($"[RenderError] PlanetViewWindow set stance threw: {ex}"); }
            }
            if (!haveTime || onCooldown) ImGui.EndDisabled();
            if (onCooldown)
            {
                ImGui.SameLine();
                ImGui.TextDisabled($"switch in {(formation.SwitchableAfter - now).TotalSeconds:0}s");
            }
        }

        private void MarchFormation(Entity body, GroundFormation formation, int target)
        {
            try
            {
                int moved = GroundForces.OrderFormationMove(body, formation, target);
                _status = moved > 0 ? $"'{formation.Name}' marches {moved} unit(s) to Region {target + 1}" : "formation couldn't march (adjacency/transit)";
            }
            catch (Exception ex) { _status = "formation march failed (logged)"; Console.WriteLine($"[RenderError] PlanetViewWindow formation march threw: {ex}"); }
        }

        // ── Detail + legend ─────────────────────────────────────────────────────────────
        private void DrawRegionDetail(Region region, GroundForcesDB forcesDB, PlanetEnvironmentsDB envDB)
        {
            ImGui.Text($"Selected view: Region {region.Index + 1}");
            if (!region.Surveyed)
            {
                ImGui.TextDisabled("Unsurveyed — scan this world to reveal its geography.");
                return;
            }
            ImGui.SameLine();
            ImGui.TextDisabled($"   {GroundTerrain.Classify(region)}   {region.Area_km2:#,##0} km²   crossing {FormatTime(region.CrossingTimeSeconds)}");

            if (envDB != null)
            {
                var hz = envDB.ForRegion(region.Index).ToList();
                if (hz.Count > 0)
                {
                    ImGui.TextColored(new Vector4(1f, 0.7f, 0.4f, 1f), "Hazards: ");
                    ImGui.SameLine();
                    ImGui.TextUnformatted(string.Join(", ", hz.Select(e => $"{e.Name} ({e.Effect})")));
                }
            }

            if (forcesDB != null)
            {
                int myFaction = _uiState.Faction?.Id ?? -1;
                var here = forcesDB.Units.Where(u => u.RegionIndex == region.Index).ToList();
                if (here.Count > 0)
                {
                    ImGui.TextDisabled("Forces here:");
                    ImGui.SameLine();
                    ImGui.TextUnformatted(string.Join(", ",
                        here.GroupBy(u => (u.FactionOwnerID, u.UnitType))
                            .Select(g => $"{g.Count()}× {g.Key.UnitType} [{OwnerLabel(g.Key.FactionOwnerID, myFaction)}]")));
                }
            }
        }

        private void DrawLegend()
        {
            ImGui.Spacing();
            ImGui.TextDisabled("Legend:  ");
            ImGui.SameLine(); ImGui.TextColored(OwnerColor(_uiState.Faction?.Id ?? -1, _uiState.Faction?.Id ?? -1), "■ yours");
            ImGui.SameLine(); ImGui.TextColored(new Vector4(0.85f, 0.25f, 0.2f, 1f), "■ hostile");
            ImGui.SameLine(); ImGui.TextDisabled("  I=Infantry A=Armor R=Artillery  »=marching");
        }

        // ── Small helpers ───────────────────────────────────────────────────────────────
        private static char TypeInitial(GroundUnitType t) => t switch
        {
            GroundUnitType.Infantry => 'I',
            GroundUnitType.Armor => 'A',
            GroundUnitType.Artillery => 'R',
            _ => '?'
        };

        private static Vector4 OwnerColor(int faction, int myFaction)
        {
            if (faction == myFaction) return new Vector4(0.2f, 0.75f, 0.9f, 1f);   // cyan = yours
            if (faction < 0) return new Vector4(0.6f, 0.6f, 0.6f, 1f);             // neutral/unowned grey
            return new Vector4(0.85f, 0.25f, 0.2f, 1f);                             // hostile red
        }

        private static string OwnerLabel(int faction, int myFaction)
        {
            if (faction == myFaction) return "You";
            if (faction < 0) return "Unowned";
            return "Hostile";
        }

        private static Vector4 HazardColor(Pulsar4X.Hazards.HazardEffectType effect) => effect switch
        {
            Pulsar4X.Hazards.HazardEffectType.HeatDamage => new Vector4(0.75f, 0.20f, 0.15f, 0.9f),
            Pulsar4X.Hazards.HazardEffectType.CorrosiveDamage => new Vector4(0.35f, 0.60f, 0.20f, 0.9f),
            Pulsar4X.Hazards.HazardEffectType.SensorJam => new Vector4(0.70f, 0.55f, 0.15f, 0.9f),
            _ => new Vector4(0.45f, 0.30f, 0.55f, 0.9f),
        };

        private static Vector4 HealthColor(float frac)
            => new Vector4(1f - frac * 0.8f, 0.2f + frac * 0.7f, 0.2f, 1f);

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
