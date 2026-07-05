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

        // G5 — the CONTINUOUS-GLOBE view: the map as a sliding WINDOW over the one cylinder grid (PlanetRegionsDB.SurfaceGrid),
        // centred on a longitude column that WRAPS at the seam. _centerCol = the window's centre column (lazily set to the
        // centre region's band centre). _globalView = use the cylinder window (default) vs. the legacy per-region disk view.
        private int _centerCol = -1;
        private bool _globalView = true;

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
                    if (!_lookedAtEntity.Entity.TryGetDataBlob<PlanetRegionsDB>(out var regionsDB)
                        || regionsDB.Regions == null || regionsDB.Regions.Count == 0)
                    {
                        ImGui.TextWrapped("This body has no surface regions (it isn't a major body, or the region layer hasn't generated).");
                    }
                    else
                    {
                        DrawTacticalMap(regionsDB.Regions);
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

            // G5 — make sure the ONE cylinder grid exists (lazy + idempotent, same pattern as the disks); the global
            // window renders it. Null on a body with no region layer → fall back to the legacy per-region disk view.
            var grid = PlanetGridFactory.EnsureGridForBody(body);
            bool canGlobal = grid != null && grid.Hexes != null && grid.Hexes.Count > 0;

            // ── Controls ────────────────────────────────────────────────────────────────
            if (ImGui.Button("◀ West")) { _centerRegion = left; SyncCenterCol(grid, count); }
            ImGui.SameLine();
            ImGui.Text($"Region {_centerRegion + 1} of {count}");
            ImGui.SameLine();
            if (ImGui.Button("East ▶")) { _centerRegion = right; SyncCenterCol(grid, count); }

            if (canGlobal)
            {
                ImGui.SameLine();
                if (ImGui.Button(_globalView ? "◑ Globe view" : "▦ Band view")) _globalView = !_globalView;
            }

            int surveyed = regions.Count(r => r.Surveyed);
            ImGui.SameLine();
            ImGui.TextDisabled($"   surveyed {surveyed}/{count}");
            if (!string.IsNullOrEmpty(_status))
            {
                ImGui.SameLine();
                ImGui.TextColored(new Vector4(0.6f, 0.9f, 0.6f, 1f), "  " + _status);
            }

            ImGui.Separator();

            // ── The map canvas: the 3-REGION HEX MAP is the default + only surface view (V3). ──
            // West | Centre | East drawn as one continuous hex field in longitude order, so V2's planet-wide coherent
            // terrain (continents/coastlines) FLOWS across the region seams; a gap + divider + label marks each
            // boundary so you can still tell where one region ends and the next begins. (Zoom to the city/fortification
            // grid is the separate C-track view.)
            bool hasHexes = _centerRegion < regions.Count && regions[_centerRegion].Hexes != null && regions[_centerRegion].Hexes.Count > 0;
            if (canGlobal && _globalView)
            {
                if (_centerCol < 0) SyncCenterCol(grid, count);   // first paint: centre on the current region's band
                DrawGlobalHexWindow(grid, regions, body, myFaction, forcesDB);
            }
            else if (hasHexes)
                DrawThreeRegionHexMap(regions, body, myFaction, forcesDB, left, _centerRegion, right);
            else
                ImGui.TextDisabled("This world isn't surveyed yet — scan it to reveal its surface hexes.");

            // ── Below the canvas: selection actions, build panel, region detail ─────────
            ImGui.Separator();
            DrawSelectionBar(regions, forcesDB, myFaction, left, right);
            DrawBuildPanel(body, myFaction);
            DrawFormationPanel(body, forcesDB, myFaction, regions, left, right);
            ImGui.Separator();
            DrawRegionDetail(regions[_centerRegion], forcesDB, envDB);
            DrawLegend();
        }

        // ── The 3-REGION HEX MAP — the default + only surface view (V3 re-apply, 2026-07-04) ─────────────
        // West | Centre | East as one continuous hex field (longitude order), so V2's coherent terrain flows across
        // the seams; a 2-hex gap + a divider line + a region label marks each boundary so regions read as distinct.
        private void DrawThreeRegionHexMap(List<Region> regions, Entity body, int myFaction, GroundForcesDB forcesDB,
            int left, int centre, int right)
        {
            var drawList = ImGui.GetWindowDrawList();
            var canvasPos = ImGui.GetCursorScreenPos();
            var canvasSize = ImGui.GetContentRegionAvail();
            float mapHeight = Math.Max(220f, canvasSize.Y * 0.66f);
            var mapSize = new Vector2(canvasSize.X, mapHeight);

            ImGui.InvisibleButton("planethexcanvas", mapSize);
            bool clicked = ImGui.IsItemHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
            var mp = ImGui.GetIO().MousePos;

            int[] cols = { left, centre, right };
            int R = 2;
            foreach (var ri in cols)
                if (regions[ri].Hexes != null && regions[ri].Hexes.Count > 0)
                    R = Math.Max(R, HexPathfinder.PatchRadius(regions[ri].Hexes));

            // ONE seamless hex field: the CENTRE region in FULL, its two neighbours bleeding in at the left/right
            // margins (only the border fraction that fits). Size so the centre patch fills the height and ≤ ~62% of the
            // width (leaving margins for the neighbours); the neighbours are offset by a full patch width (seamless — no
            // gap) and CULLED to the visible canvas, so you see the whole centre and just the overlapping edge of each side.
            float span = 2 * R + 1.6f;
            int qStep = 2 * R + 1;                     // full patch width → the side regions sit flush against the centre
            float sizeH = mapSize.Y / (1.5f * span);
            float sizeW = mapSize.X * 0.62f / (1.7320508f * span);
            float size = Math.Max(3f, Math.Min(sizeH, sizeW));
            var center = new Vector2(canvasPos.X + mapSize.X * 0.5f, canvasPos.Y + mapSize.Y * 0.5f);
            uint hexBorder = ImGui.ColorConvertFloat4ToU32(new Vector4(0f, 0f, 0f, 0.30f));
            float xLo = canvasPos.X - size, xHi = canvasPos.X + mapSize.X + size;   // cull neighbours to the visible canvas

            // 1) terrain hexes — each region offset into one shared field, so continents span the seams.
            for (int c = 0; c < 3; c++)
            {
                var region = regions[cols[c]];
                if (region.Hexes == null) continue;
                int qOff = (c - 1) * qStep;
                foreach (var h in region.Hexes)
                {
                    var pc = HexCenter(center, size, h.Q + qOff, h.R);
                    if (pc.X < xLo || pc.X > xHi) continue;   // neighbour hexes past the margin aren't drawn
                    Vector4 col = region.Surveyed
                        ? (_featureColors.TryGetValue(h.Terrain, out var vc) ? vc : _featureColors[RegionFeatureType.Barren])
                        : _featureColors[RegionFeatureType.Unknown];
                    drawList.AddNgonFilled(pc, size, ImGui.ColorConvertFloat4ToU32(col), 6);
                    if (size > 5f) drawList.AddNgon(pc, size, hexBorder, 6, 1f);
                }
            }

            // 2) units on their hexes (grouped per hex + faction, coloured by owner).
            for (int c = 0; c < 3; c++)
            {
                var region = regions[cols[c]];
                int qOff = (c - 1) * qStep;
                var occ = new Dictionary<(int q, int r, int fac), (int n, int type, int moving)>();
                if (forcesDB != null)
                    foreach (var u in forcesDB.Units)
                    {
                        if (u.RegionIndex != region.Index) continue;
                        var key = (u.HexQ, u.HexR, u.FactionOwnerID);
                        occ.TryGetValue(key, out var g); g.n++; g.type = (int)u.UnitType;
                        if (u.HexPath != null && u.HexPath.Count > 0) g.moving++;
                        occ[key] = g;
                    }
                foreach (var kv in occ)
                {
                    var pc = HexCenter(center, size, kv.Key.q + qOff, kv.Key.r);
                    if (pc.X < xLo || pc.X > xHi) continue;
                    float mr = Math.Max(2.5f, size * 0.55f);
                    drawList.AddCircleFilled(pc, mr, ImGui.ColorConvertFloat4ToU32(OwnerColor(kv.Key.fac, myFaction)), 16);
                    drawList.AddCircle(pc, mr, ImGui.ColorConvertFloat4ToU32(new Vector4(0f, 0f, 0f, 0.8f)), 16, 1.5f);
                    if (size > 8f)
                    {
                        string tag = $"{TypeInitial((GroundUnitType)kv.Value.type)}{kv.Value.n}" + (kv.Value.moving > 0 ? "»" : "");
                        var tsz = ImGui.CalcTextSize(tag);
                        drawList.AddText(new Vector2(pc.X - tsz.X * 0.5f, pc.Y - tsz.Y * 0.5f),
                            ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 1f, 1f, 1f)), tag);
                    }
                    if (HasSelection && _selRegion == region.Index && _selFaction == kv.Key.fac && _selType == (GroundUnitType)kv.Value.type)
                        drawList.AddNgon(pc, size, ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 1f, 0.3f, 1f)), 6, 2.5f);
                }
            }

            // 3) a subtle seam line at each EDGE of the centre region + region labels — so you can still tell where the
            //    centre ends and a neighbour begins, without breaking the seamless look.
            uint divCol = ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 0.95f, 0.6f, 0.45f));
            float seamW = HexCenter(center, size, -R, 0).X - size * 0.87f;   // just outside the centre's west edge
            float seamE = HexCenter(center, size,  R, 0).X + size * 0.87f;   // just outside the centre's east edge
            if (seamW > canvasPos.X) drawList.AddLine(new Vector2(seamW, canvasPos.Y + 18f), new Vector2(seamW, canvasPos.Y + mapHeight), divCol, 2f);
            if (seamE < canvasPos.X + mapSize.X) drawList.AddLine(new Vector2(seamE, canvasPos.Y + 18f), new Vector2(seamE, canvasPos.Y + mapHeight), divCol, 2f);

            DrawRegionLabel(drawList, $"R{regions[centre].Index + 1} (centre)", center.X, canvasPos, mapSize, true);
            DrawRegionLabel(drawList, $"◂ R{regions[left].Index + 1}", canvasPos.X + 4f, canvasPos, mapSize, false);
            DrawRegionLabel(drawList, $"R{regions[right].Index + 1} ▸", canvasPos.X + mapSize.X - 4f, canvasPos, mapSize, false);

            // 4) click resolution — centre region = full hex ops; a side region = coarse-march there (if adjacent) or recentre.
            if (clicked)
            {
                GroundHex best = null; int bestRegion = -1; float bestD = size * 1.2f;
                for (int c = 0; c < 3; c++)
                {
                    var region = regions[cols[c]];
                    if (region.Hexes == null) continue;
                    int qOff = (c - 1) * qStep;
                    foreach (var h in region.Hexes)
                    {
                        var pc = HexCenter(center, size, h.Q + qOff, h.R);
                        if (pc.X < xLo || pc.X > xHi) continue;
                        float d = Vector2.Distance(pc, mp);
                        if (d < bestD) { bestD = d; best = h; bestRegion = region.Index; }
                    }
                }
                if (best != null)
                {
                    if (bestRegion == centre)
                        HandleHexClick(regions[centre], body, myFaction, forcesDB, best);
                    else if (HasSelection && _selFaction == myFaction && _selRegion != bestRegion
                             && _selRegion < regions.Count && regions[_selRegion].Neighbors.Contains(bestRegion))
                        MarchSelectedTo(regions, bestRegion);
                    else
                        _centerRegion = bestRegion;
                }
            }

            // 5) caption + the H3 range readout for the selected group.
            ImGui.Text($"Surface — West ◂ Region {regions[centre].Index + 1} ▸ East   (click a hex; click a side region to go there)");
            if (HasSelection && _selFaction == myFaction)
            {
                var rep = forcesDB?.Units.FirstOrDefault(u => u.RegionIndex == _selRegion && u.FactionOwnerID == myFaction && u.UnitType == _selType);
                int range = rep?.Range ?? 0;
                double km = _selRegion < regions.Count ? GroundRangeTools.RealReachKm(range, regions[_selRegion]) : 0;
                ImGui.TextDisabled($"Selected {_selType} in Region {_selRegion + 1}: click a destination hex (same region) to march — strike range {range} hex ≈ {km:N0} km. Ocean impassable.");
            }
        }

        private static void DrawRegionLabel(ImDrawListPtr drawList, string lbl, float x, Vector2 canvasPos, Vector2 mapSize, bool isCentre)
        {
            var lsz = ImGui.CalcTextSize(lbl);
            float px = Math.Clamp(x - lsz.X * 0.5f, canvasPos.X + 2f, canvasPos.X + mapSize.X - lsz.X - 2f);
            drawList.AddText(new Vector2(px, canvasPos.Y + 2f),
                ImGui.ColorConvertFloat4ToU32(isCentre ? new Vector4(1f, 1f, 0.7f, 1f) : new Vector4(0.75f, 0.75f, 0.8f, 1f)), lbl);
        }

        // ── G5: the CONTINUOUS-GLOBE window — a sliding view over the ONE cylinder grid (SurfaceGrid) ─────────────
        // The payoff of the global grid: instead of three stitched disks, the map is a WINDOW onto one wrapping world.
        // It's centred on a longitude column (_centerCol) and shows a slice ~2 region-bands wide — the centre band in
        // full, the neighbouring bands bleeding in at the margins — and WRAPS at the seam, so any place shows up in
        // every window whose longitude reaches it. Terrain is continuous by construction; a faint seam line + label
        // marks each region-band boundary. Units draw at their GLOBAL (Q,R); click a hex to select/march
        // (GroundForces.OrderMoveToGlobalHex — no edge gates).
        private void DrawGlobalHexWindow(SurfaceGrid grid, List<Region> regions, Entity body, int myFaction, GroundForcesDB forcesDB)
        {
            int cols = grid.Cols, rows = grid.Rows, rc = Math.Max(1, regions.Count);
            int bandW = Math.Max(1, cols / rc);
            int halfW = bandW;                       // window half-width in columns → ~2 bands visible (centre full + neighbours' inner halves)
            int winCols = 2 * halfW + 1;

            var drawList = ImGui.GetWindowDrawList();
            var canvasPos = ImGui.GetCursorScreenPos();
            var canvasSize = ImGui.GetContentRegionAvail();
            float mapHeight = Math.Max(220f, canvasSize.Y * 0.66f);
            var mapSize = new Vector2(canvasSize.X, mapHeight);

            ImGui.InvisibleButton("planetglobecanvas", mapSize);
            bool clicked = ImGui.IsItemHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
            var mp = ImGui.GetIO().MousePos;

            // Size a hex so the window (winCols × rows, odd-r offset) fits, then centre it in the canvas.
            float sizeW = mapSize.X / ((winCols + 0.5f) * 1.7320508f);
            float sizeH = mapSize.Y / (rows * 1.5f + 0.5f);
            float size = Math.Max(2f, Math.Min(sizeW, sizeH));
            float gridW = (winCols + 0.5f) * 1.7320508f * size;
            float gridH = (rows * 1.5f + 0.5f) * size;
            var origin = new Vector2(
                canvasPos.X + (mapSize.X - gridW) * 0.5f + 1.7320508f * size,
                canvasPos.Y + (mapSize.Y - gridH) * 0.5f + size);

            uint hexBorder = ImGui.ColorConvertFloat4ToU32(new Vector4(0f, 0f, 0f, 0.28f));

            // 1) terrain — every visible column (wraps), every row.
            for (int sc = 0; sc < winCols; sc++)
            {
                int gq = grid.WrapCol(_centerCol - halfW + sc);
                for (int r = 0; r < rows; r++)
                {
                    var h = grid.HexAt(gq, r);
                    if (h == null) continue;
                    var pc = HexCenterOffset(origin, size, sc, r);
                    var col = _featureColors.TryGetValue(h.Terrain, out var vc) ? vc : _featureColors[RegionFeatureType.Barren];
                    drawList.AddNgonFilled(pc, size, ImGui.ColorConvertFloat4ToU32(col), 6);
                    if (size > 4f) drawList.AddNgon(pc, size, hexBorder, 6, 1f);
                }
            }

            // 2) region-band seam lines + centre label (a band boundary is where RegionOfColumn changes).
            uint seamCol = ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 0.95f, 0.6f, 0.4f));
            for (int sc = 0; sc <= winCols; sc++)
            {
                int gqL = grid.WrapCol(_centerCol - halfW + sc - 1);
                int gqR = grid.WrapCol(_centerCol - halfW + sc);
                if (PlanetGridFactory.RegionOfColumn(gqL, cols, rc) != PlanetGridFactory.RegionOfColumn(gqR, cols, rc))
                {
                    float x = HexCenterOffset(origin, size, sc, 0).X - 1.7320508f * size * 0.5f;
                    drawList.AddLine(new Vector2(x, canvasPos.Y + 16f), new Vector2(x, canvasPos.Y + mapHeight), seamCol, 1.5f);
                }
            }
            int centreBand = PlanetGridFactory.RegionOfColumn(_centerCol, cols, rc);
            DrawRegionLabel(drawList, $"R{regions[centreBand].Index + 1} (centre)", canvasPos.X + mapSize.X * 0.5f, canvasPos, mapSize, true);

            // 3) units at their GLOBAL (Q,R), grouped per (gq, gr, faction).
            var occ = new Dictionary<(int gq, int gr, int fac), (int n, int type, int moving)>();
            if (forcesDB != null)
                foreach (var u in forcesDB.Units)
                {
                    if (u.GlobalQ < 0 || u.GlobalR < 0) continue;
                    int dc = WrapDelta(u.GlobalQ - _centerCol, cols);
                    if (dc < -halfW || dc > halfW) continue;    // outside the window
                    var key = (u.GlobalQ, u.GlobalR, u.FactionOwnerID);
                    occ.TryGetValue(key, out var g); g.n++; g.type = (int)u.UnitType;
                    if (u.GlobalPath != null && u.GlobalPath.Count > 0) g.moving++;
                    occ[key] = g;
                }
            foreach (var kv in occ)
            {
                int dc = WrapDelta(kv.Key.gq - _centerCol, cols);
                int sc = dc + halfW;
                var pc = HexCenterOffset(origin, size, sc, kv.Key.gr);
                float mr = Math.Max(2.5f, size * 0.55f);
                drawList.AddCircleFilled(pc, mr, ImGui.ColorConvertFloat4ToU32(OwnerColor(kv.Key.fac, myFaction)), 16);
                drawList.AddCircle(pc, mr, ImGui.ColorConvertFloat4ToU32(new Vector4(0f, 0f, 0f, 0.8f)), 16, 1.5f);
                if (size > 8f)
                {
                    string tag = $"{TypeInitial((GroundUnitType)kv.Value.type)}{kv.Value.n}" + (kv.Value.moving > 0 ? "»" : "");
                    var tsz = ImGui.CalcTextSize(tag);
                    drawList.AddText(new Vector2(pc.X - tsz.X * 0.5f, pc.Y - tsz.Y * 0.5f),
                        ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 1f, 1f, 1f)), tag);
                }
                if (HasSelection && _selFaction == kv.Key.fac && _selType == (GroundUnitType)kv.Value.type
                    && _selRegion == PlanetGridFactory.RegionOfColumn(kv.Key.gq, cols, rc))
                    drawList.AddNgon(pc, size, ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 1f, 0.3f, 1f)), 6, 2.5f);
            }

            // 4) click → nearest visible hex → select your units there / march a selected group there (global A*).
            if (clicked)
            {
                int bestGq = -1, bestR = -1; float bestD = size * 1.3f;
                for (int sc = 0; sc < winCols; sc++)
                {
                    int gq = grid.WrapCol(_centerCol - halfW + sc);
                    for (int r = 0; r < rows; r++)
                    {
                        float d = Vector2.Distance(HexCenterOffset(origin, size, sc, r), mp);
                        if (d < bestD) { bestD = d; bestGq = gq; bestR = r; }
                    }
                }
                if (bestGq >= 0) HandleGlobalHexClick(grid, regions, body, myFaction, forcesDB, bestGq, bestR);
            }

            // 5) caption + the H3 range readout for the selected group.
            ImGui.Text($"Surface (globe) — one continuous world, centred on Region {regions[centreBand].Index + 1}; ◀/▶ pans, click a hex to select/march.");
            if (HasSelection && _selFaction == myFaction && _selRegion < regions.Count)
            {
                var rep = forcesDB?.Units.FirstOrDefault(u => u.RegionIndex == _selRegion && u.FactionOwnerID == myFaction && u.UnitType == _selType);
                int range = rep?.Range ?? 0;
                double km = GroundRangeTools.RealReachKm(range, regions[_selRegion]);
                ImGui.TextDisabled($"Selected {_selType} (Region {_selRegion + 1}): click a destination hex to march — strike range {range} hex ≈ {km:N0} km. Ocean impassable.");
            }
        }

        /// <summary>Click on a GLOBAL hex: select your units standing there (toggle), or march the selected group there
        /// via the wrapping global A* (no edge gates). Also recentres the region-based panels on the clicked band.</summary>
        private void HandleGlobalHexClick(SurfaceGrid grid, List<Region> regions, Entity body, int myFaction, GroundForcesDB forcesDB, int gq, int gr)
        {
            int band = PlanetGridFactory.RegionOfColumn(gq, grid.Cols, Math.Max(1, regions.Count));
            _centerRegion = band;                     // keep the region-based panels tracking what you clicked

            var mineHere = forcesDB?.Units.FirstOrDefault(u => u.GlobalQ == gq && u.GlobalR == gr && u.FactionOwnerID == myFaction);
            if (mineHere != null)
            {
                if (HasSelection && _selFaction == myFaction && _selRegion == mineHere.RegionIndex && _selType == mineHere.UnitType)
                    ClearSelection();
                else { _selRegion = mineHere.RegionIndex; _selFaction = myFaction; _selType = mineHere.UnitType; _status = ""; }
                return;
            }
            if (HasSelection && _selFaction == myFaction)
                MoveSelectedToGlobalHex(body, forcesDB, gq, gr);
        }

        /// <summary>March every orderable unit in the selected group to a target GLOBAL hex (wrapping A* per unit).</summary>
        private void MoveSelectedToGlobalHex(Entity body, GroundForcesDB forcesDB, int destQ, int destR)
        {
            if (forcesDB == null || _selFaction != (_uiState.Faction?.Id ?? -1)) { _status = "those aren't your units"; return; }
            int moved = 0;
            foreach (var u in forcesDB.Units.ToArray())
            {
                if (u.RegionIndex != _selRegion || u.FactionOwnerID != _selFaction || u.UnitType != _selType) continue;
                if (u.MovingToRegion >= 0) continue;
                if (GroundForces.OrderMoveToGlobalHex(body, u, destQ, destR)) moved++;
            }
            _status = moved > 0
                ? $"marched {moved}× {_selType} → global hex ({destQ},{destR})"
                : "no march (unreachable / impassable water / already there)";
        }

        /// <summary>Recentre the globe window on a region's band-centre column (keeps ◀/▶ + the region panels in sync).</summary>
        private void SyncCenterCol(SurfaceGrid grid, int regionCount)
        {
            if (grid == null || grid.Cols <= 0) { _centerCol = 0; return; }
            _centerCol = PlanetGridFactory.BandCentreColumn(_centerRegion, grid.Cols, Math.Max(1, regionCount));
        }

        /// <summary>Signed shortest column delta on the cylinder (wraps): result in [-cols/2, cols/2].</summary>
        private static int WrapDelta(int d, int cols)
        {
            if (cols <= 0) return d;
            d = ((d % cols) + cols) % cols;
            if (d > cols / 2) d -= cols;
            return d;
        }

        /// <summary>Odd-r OFFSET hex layout (pointy-top) — screen position of window column <paramref name="sc"/>, row
        /// <paramref name="r"/>. Unlike the axial <see cref="HexCenter"/> (which shears with r), this keeps the grid
        /// RECTANGULAR (odd rows nudged half a hex), so a wide cylinder window reads as a proper map — matching the
        /// odd-r neighbour model the global pathfinder (G2) uses.</summary>
        private static Vector2 HexCenterOffset(Vector2 origin, float size, int sc, int r)
            => new Vector2(origin.X + size * 1.7320508f * (sc + 0.5f * (r & 1)), origin.Y + size * 1.5f * r);

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
                    DrawRoeSelector(f);
                    DrawOrderQueue(body, forcesDB, f, regions, left, right);
                }
            }
        }

        // ── The ORDER QUEUE (O1) — build a sequential plan for the formation ("move → move → dig in") ──
        private void DrawOrderQueue(Entity body, GroundForcesDB forcesDB, GroundFormation f, List<Region> regions, int left, int right)
        {
            ImGui.Separator();
            if (f.Orders != null && f.Orders.Count > 0)
            {
                ImGui.TextColored(new Vector4(0.8f, 0.9f, 1f, 1f), $"Plan ({f.Orders.Count} order{(f.Orders.Count == 1 ? "" : "s")}):");
                for (int i = 0; i < f.Orders.Count; i++)
                    ImGui.TextDisabled($"   {i + 1}. {f.Orders[i].Describe()}");
                if (ImGui.Button($"Clear plan##oq{f.FormationId}")) { GroundForces.ClearFormationOrders(f); _status = "plan cleared"; }
            }
            else
            {
                ImGui.TextDisabled("No queued plan. Add orders below, or Shift-click a hex (Hex view) to add a move waypoint.");
            }

            // Queue MOVE-to-region waypoints (visible ring neighbours of the formation's rally region).
            int rally = GroundForces.LeaderRegion(forcesDB, f);
            var rallyRegion = (rally >= 0 && rally < regions.Count) ? regions[rally] : null;
            if (rallyRegion != null)
            {
                if (rallyRegion.Neighbors.Contains(left) && ImGui.Button($"+ March → R{left + 1}##oq{f.FormationId}"))
                { GroundForces.QueueFormationOrder(f, GroundOrder.MoveRegion(left)); _status = $"queued → region {left + 1}"; }
                if (rallyRegion.Neighbors.Contains(left)) ImGui.SameLine();
                if (rallyRegion.Neighbors.Contains(right) && ImGui.Button($"+ March → R{right + 1}##oq{f.FormationId}"))
                { GroundForces.QueueFormationOrder(f, GroundOrder.MoveRegion(right)); _status = $"queued → region {right + 1}"; }
                if (rallyRegion.Neighbors.Contains(right)) ImGui.SameLine();
            }

            // Queue non-spatial orders (a timed hold + ROE switches — "then dig in / then stand off").
            if (ImGui.Button($"+ Hold 6h##oq{f.FormationId}")) { GroundForces.QueueFormationOrder(f, GroundOrder.Hold(6 * 3600)); _status = "queued hold 6h"; }
            ImGui.SameLine();
            if (ImGui.Button($"+ ROE Stand-off##oq{f.FormationId}")) { GroundForces.QueueFormationOrder(f, GroundOrder.Roe(GroundEngagementStance.StandOff)); _status = "queued ROE stand-off"; }
            ImGui.SameLine();
            if (ImGui.Button($"+ ROE Close##oq{f.FormationId}")) { GroundForces.QueueFormationOrder(f, GroundOrder.Roe(GroundEngagementStance.CloseToEngage)); _status = "queued ROE close"; }
        }

        // The RULES OF ENGAGEMENT selector — the commander's maneuver intent (the ground echo of the space
        // engagement-posture selector). Hold / Close / Stand-off; applied immediately (no cooldown — an intent).
        private void DrawRoeSelector(GroundFormation formation)
        {
            var stances = new[] { GroundEngagementStance.HoldGround, GroundEngagementStance.CloseToEngage, GroundEngagementStance.StandOff };
            var names = new[] { "Hold Ground", "Close to Engage", "Stand Off (auto-kite)" };
            int cur = Array.IndexOf(stances, formation.Engagement);
            if (cur < 0) cur = 0;

            ImGui.TextDisabled("ROE:");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(190f);
            int choice = cur;
            if (ImGui.Combo($"##roe{formation.FormationId}", ref choice, names, names.Length) && choice != cur && choice >= 0 && choice < stances.Length)
            {
                try
                {
                    GroundFormationDoctrine.SetEngagementStance(formation, stances[choice]);
                    _status = $"ROE: {names[choice]}";
                }
                catch (Exception ex) { _status = "set ROE failed (logged)"; Console.WriteLine($"[RenderError] PlanetViewWindow set ROE threw: {ex}"); }
            }
            ImGui.SameLine();
            ImGui.TextDisabled(formation.Engagement == GroundEngagementStance.StandOff ? "(kites to hold range)"
                : formation.Engagement == GroundEngagementStance.CloseToEngage ? "(advances to contact)"
                : "(stands and fights)");
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

        // ── H4: the HEX drill-in — the centre region's fine grid (Planet → Region → Hex) ─
        private void DrawHexMap(Region region, Entity body, int myFaction, GroundForcesDB forcesDB)
        {
            var hexes = region.Hexes;
            if (hexes == null || hexes.Count == 0) { ImGui.TextDisabled("This region has no hex detail yet."); return; }

            var drawList = ImGui.GetWindowDrawList();
            var canvasPos = ImGui.GetCursorScreenPos();
            var canvasSize = ImGui.GetContentRegionAvail();
            float mapHeight = Math.Max(200f, canvasSize.Y * 0.62f);
            var mapSize = new Vector2(canvasSize.X, mapHeight);

            ImGui.InvisibleButton("hexcanvas", mapSize);
            bool clicked = ImGui.IsItemHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
            var mp = ImGui.GetIO().MousePos;

            // Size a hex so the whole patch fits (a radius-R disk spans ~2R+1 hexes each way).
            int R = HexPathfinder.PatchRadius(hexes);
            float span = 2 * R + 1.6f;
            float size = Math.Max(4f, Math.Min(mapSize.X / (1.7320508f * span), mapSize.Y / (1.5f * span)));
            var center = new Vector2(canvasPos.X + mapSize.X * 0.5f, canvasPos.Y + mapSize.Y * 0.5f);

            uint hexBorder = ImGui.ColorConvertFloat4ToU32(new Vector4(0f, 0f, 0f, 0.35f));
            foreach (var h in hexes)
            {
                var c = HexCenter(center, size, h.Q, h.R);
                var col = _featureColors.TryGetValue(h.Terrain, out var vc) ? vc : _featureColors[RegionFeatureType.Barren];
                drawList.AddNgonFilled(c, size, ImGui.ColorConvertFloat4ToU32(col), 6);
                drawList.AddNgon(c, size, hexBorder, 6, 1f);
            }

            // Units stacked on their hex, grouped by (hex, faction) — a marker + count, coloured by owner.
            var occ = new Dictionary<(int q, int r, int fac), (int n, int type, int moving)>();
            if (forcesDB != null)
            {
                foreach (var u in forcesDB.Units)
                {
                    if (u.RegionIndex != region.Index) continue;
                    var key = (u.HexQ, u.HexR, u.FactionOwnerID);
                    occ.TryGetValue(key, out var g);
                    g.n += 1; g.type = (int)u.UnitType;
                    if (u.HexPath != null && u.HexPath.Count > 0) g.moving += 1;
                    occ[key] = g;
                }
            }
            foreach (var kv in occ)
            {
                var c = HexCenter(center, size, kv.Key.q, kv.Key.r);
                float mr = Math.Max(3f, size * 0.55f);
                drawList.AddCircleFilled(c, mr, ImGui.ColorConvertFloat4ToU32(OwnerColor(kv.Key.fac, myFaction)), 16);
                drawList.AddCircle(c, mr, ImGui.ColorConvertFloat4ToU32(new Vector4(0f, 0f, 0f, 0.8f)), 16, 1.5f);
                if (size > 8f)
                {
                    string tag = $"{TypeInitial((GroundUnitType)kv.Value.type)}{kv.Value.n}" + (kv.Value.moving > 0 ? "»" : "");
                    var tsz = ImGui.CalcTextSize(tag);
                    drawList.AddText(new Vector2(c.X - tsz.X * 0.5f, c.Y - tsz.Y * 0.5f),
                        ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 1f, 1f, 1f)), tag);
                }
                // Highlight the hexes holding the selected group.
                if (HasSelection && _selRegion == region.Index && _selFaction == kv.Key.fac && _selType == (GroundUnitType)kv.Value.type)
                    drawList.AddNgon(c, size, ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 1f, 0.3f, 1f)), 6, 2.5f);
            }

            // Click: pick the nearest hex centre to the cursor (robust vs. exact polygon hit-testing).
            if (clicked)
            {
                GroundHex best = null; float bestD = size * 1.2f;
                foreach (var h in hexes)
                {
                    float d = Vector2.Distance(HexCenter(center, size, h.Q, h.R), mp);
                    if (d < bestD) { bestD = d; best = h; }
                }
                if (best != null) HandleHexClick(region, body, myFaction, forcesDB, best);
            }

            // Caption + the H3 range readout for the selected group (hex range → real km on THIS body).
            ImGui.Text($"Hex view — Region {region.Index + 1}  ({hexes.Count} hexes)");
            if (HasSelection && _selFaction == myFaction)
            {
                var rep = forcesDB?.Units.FirstOrDefault(u => u.RegionIndex == _selRegion && u.FactionOwnerID == myFaction && u.UnitType == _selType);
                int range = rep?.Range ?? 0;
                double km = GroundRangeTools.RealReachKm(range, region);
                ImGui.TextDisabled($"Selected {_selType}: click a destination hex to march (strike range {range} hex ≈ {km:N0} km). Ocean is impassable.");
            }
            else
            {
                ImGui.TextDisabled("Click a hex with your units to select them, then click a destination hex to march them there.");
            }
        }

        /// <summary>Click on a hex: SHIFT-click with a formation selected QUEUES a move waypoint (build a plan);
        /// otherwise select your units standing there, or (with a group selected) march it there now.</summary>
        private void HandleHexClick(Region region, Entity body, int myFaction, GroundForcesDB forcesDB, GroundHex hex)
        {
            // Shift-click with a formation selected → append a move waypoint to its plan (RTS-style queueing).
            if (ImGui.GetIO().KeyShift && _selFormationId >= 0 && forcesDB != null)
            {
                var sf = forcesDB.Formations.FirstOrDefault(x => x.FormationId == _selFormationId && x.FactionOwnerID == myFaction);
                if (sf != null)
                {
                    GroundForces.QueueFormationOrder(sf, GroundOrder.MoveHex(hex.Q, hex.R));
                    _status = $"queued → hex ({hex.Q},{hex.R})  [{sf.Orders.Count} in plan]";
                    return;
                }
            }

            var mineHere = forcesDB?.Units.FirstOrDefault(u =>
                u.RegionIndex == region.Index && u.HexQ == hex.Q && u.HexR == hex.R && u.FactionOwnerID == myFaction);
            if (mineHere != null)
            {
                // Toggle selection of your units on this hex (by type — the group model the region view uses).
                if (HasSelection && _selRegion == region.Index && _selFaction == myFaction && _selType == mineHere.UnitType)
                    ClearSelection();
                else { _selRegion = region.Index; _selFaction = myFaction; _selType = mineHere.UnitType; _status = ""; }
                return;
            }
            // Empty (or enemy-only) hex + a selection of yours in this region → march there.
            if (HasSelection && _selFaction == myFaction && _selRegion == region.Index)
                MoveSelectedToHex(body, hex.Q, hex.R);
        }

        /// <summary>March every orderable unit in the selected group to a target HEX within its region (A* per unit).</summary>
        private void MoveSelectedToHex(Entity body, int destQ, int destR)
        {
            if (!body.TryGetDataBlob<GroundForcesDB>(out var forcesDB)) return;
            if (_selFaction != (_uiState.Faction?.Id ?? -1)) { _status = "those aren't your units"; return; }

            int moved = 0;
            foreach (var u in forcesDB.Units.ToArray())
            {
                if (u.RegionIndex != _selRegion || u.FactionOwnerID != _selFaction || u.UnitType != _selType) continue;
                if (u.MovingToRegion >= 0) continue;   // busy on a coarse region hop
                if (GroundForces.OrderMoveToHex(body, u, destQ, destR)) moved++;
            }
            _status = moved > 0
                ? $"marched {moved}× {_selType} to hex ({destQ},{destR})"
                : "no march (unreachable / impassable water / already there)";
        }

        /// <summary>Axial hex (q,r) → screen position (pointy-top layout), centred on <paramref name="origin"/>.</summary>
        private static Vector2 HexCenter(Vector2 origin, float size, int q, int r)
            => new Vector2(origin.X + size * 1.7320508f * (q + r * 0.5f), origin.Y + size * 1.5f * r);

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
