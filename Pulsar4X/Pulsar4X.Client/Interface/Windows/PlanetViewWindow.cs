using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ImGuiNET;
using Pulsar4X.Engine;
using Pulsar4X.Galaxy;
using Pulsar4X.GeoSurveys;
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

        // G5/G6 — the CONTINUOUS-GLOBE view: the map is a sliding WINDOW over the one cylinder grid
        // (PlanetRegionsDB.SurfaceGrid), centred on a longitude column that WRAPS at the seam. _centerCol = the window's
        // centre column (lazily set to the centre region's band centre). This is the ONLY surface view now — G6 retired
        // the old per-region disk ("band") view the developer asked to drop.
        private int _centerCol = -1;

        // C-track — the CITY zoom: which GLOBAL operational hex we've zoomed into (mini-hex grid under it). -1 = not
        // zoomed (globe view). Double-click a hex on the globe to zoom in; "Back to globe" clears it.
        private int _zoomQ = -1;
        private int _zoomR = -1;
        // The armed building for per-tile placement in the city zoom (combo index into the hex's un-placed footprint
        // buildings; 0 = none). Click an empty mini-hex tile with one armed to place it there.
        private int _placeChoice = 0;
        // The installation design armed to BUILD-here (queue a real production job) in the city zoom; 0 = none.
        private int _buildInstallChoice = 0;

        // The selected unit GROUP (region, owner faction, type) — the thing a March order acts on. -1 region = none.
        private int _selRegion = -1;
        private int _selFaction = -1;
        private GroundUnitType _selType = GroundUnitType.Infantry;

        // The last GLOBAL hex the player clicked — the target for "build a mine on this deposit". -1 = none.
        private int _selGQ = -1;
        private int _selGR = -1;

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

            // G5/G6 — the ONE cylinder grid is the surface view. Lazy + idempotent (same pattern as the old disks);
            // null only on a body with no region layer (then there's nothing to draw yet).
            var grid = PlanetGridFactory.EnsureGridForBody(body);
            // Only reveal a world's surface once THE VIEWING FACTION has geo-surveyed it (home starts surveyed; a
            // fogged world stays hidden until you scan it). Gate on the PER-FACTION GeoSurveyStatus, NOT the shared
            // Region.Surveyed flag: that flag is faction-agnostic (v1), so a RIVAL colonizing a body — e.g. the UMF on
            // Luna/Venus — flips it for EVERYONE and would leak the surface to you. GeoSurveyStatus is keyed by faction,
            // so it correctly reads "surveyed" only for the world you actually scanned/settled.
            //
            // BUT: a body authored with NO survey requirement carries NO GeoSurveyableDB at all — the HOMEWORLD (Earth)
            // is the one such body (its blueprint omits GeoSurveyPointsRequired, because your own capital was never meant
            // to need surveying; every other Sol body has the gauge). With no gauge, IsSurveyComplete can never read true,
            // so the old `&&` form silently fogged Earth forever — "This world isn't surveyed yet" with no hexes (the
            // regression when the surface view moved from the band view, which checked the always-set Region.Surveyed).
            // Fix: treat "no survey gauge fitted" as "no survey gate applies / already known" — Earth (and any gauge-less
            // body) renders immediately, while a body that HAS the gauge still requires the viewing faction to complete it
            // (the anti-leak rule above holds — rival bodies all carry the gauge). The `||` short-circuits, so geoDB is
            // only read when TryGet actually succeeded — cannot NRE.
            bool playerSurveyed = !body.TryGetDataBlob<GeoSurveyableDB>(out var geoDB) || geoDB.IsSurveyComplete(myFaction);
            bool canGlobal = playerSurveyed && grid != null && grid.Hexes != null && grid.Hexes.Count > 0;

            // ── Controls ────────────────────────────────────────────────────────────────
            if (ImGui.Button("◀ West")) { _centerRegion = left; SyncCenterCol(grid, count); }
            ImGui.SameLine();
            ImGui.Text($"Region {_centerRegion + 1} of {count}");
            ImGui.SameLine();
            if (ImGui.Button("East ▶")) { _centerRegion = right; SyncCenterCol(grid, count); }

            int surveyed = regions.Count(r => r.Surveyed);
            ImGui.SameLine();
            ImGui.TextDisabled($"   surveyed {surveyed}/{count}");
            if (!string.IsNullOrEmpty(_status))
            {
                ImGui.SameLine();
                ImGui.TextColored(new Vector4(0.6f, 0.9f, 0.6f, 1f), "  " + _status);
            }

            ImGui.Separator();

            // ── The map canvas: the CONTINUOUS-GLOBE window is the surface view (G6, the developer's call). ──
            // One wrapping cylinder grid rendered as a sliding window — the centre band in full, its neighbours bleeding
            // in at the margins, seamless at the wrap — so terrain FLOWS across region boundaries and any place shows up
            // in every window whose longitude reaches it. (The zoomed city/fortification grid is the separate C-track view.)
            if (canGlobal && _zoomQ >= 0)
                DrawCityZoom(grid, regions, body, myFaction);      // C-track: zoomed into one operational hex's mini-hex grid
            else if (canGlobal)
            {
                if (_centerCol < 0) SyncCenterCol(grid, count);   // first paint: centre on the current region's band
                DrawGlobalHexWindow(grid, regions, body, myFaction, forcesDB);
            }
            else
                ImGui.TextDisabled("This world isn't surveyed yet — scan it to reveal its surface.");

            // ── Below the canvas: selection actions, build panel, region detail ─────────
            ImGui.Separator();
            DrawSelectionBar(regions, forcesDB, myFaction, left, right);
            DrawBuildPanel(body, myFaction);
            DrawFormationPanel(body, forcesDB, myFaction, regions, left, right);
            ImGui.Separator();
            DrawRegionDetail(regions[_centerRegion], forcesDB, envDB);
            DrawLegend();
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
        /// <summary>Resolve mineral id → name for the deposit labels, defensively (never throws; tolerates a viewed
        /// faction with no/locked mineral data — gotcha #10/#11). Cheap (~15 entries), rebuilt per frame.</summary>
        private Dictionary<int, string> BuildMineralNames()
        {
            var names = new Dictionary<int, string>();
            try
            {
                var faction = _uiState.Faction;
                if (faction != null && faction.TryGetDataBlob<Pulsar4X.Factions.FactionInfoDB>(out var fi)
                    && fi.Data?.CargoGoods != null)
                    foreach (var m in fi.Data.CargoGoods.GetMineralsList())
                        if (m != null) names[m.ID] = m.Name;
            }
            catch { }
            return names;
        }

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
            bool dbl = ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left);   // double-click a hex → zoom into its city
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

            // Mineral deposit names (id → name), resolved once, defensively (tolerant of foreign/locked faction data).
            var mineralNames = BuildMineralNames();

            // 1) terrain + located mineral DEPOSITS — every visible column (wraps), every row.
            for (int sc = 0; sc < winCols; sc++)
            {
                int gq = grid.WrapCol(_centerCol - halfW + sc);
                // Deposits are only known POST-SCAN: show them only where this column's region band is surveyed
                // (home starts surveyed; a fogged world reveals its deposits once you geo-survey it).
                int band = PlanetGridFactory.RegionOfColumn(gq, cols, rc);
                bool bandSurveyed = band >= 0 && band < regions.Count && regions[band].Surveyed;
                for (int r = 0; r < rows; r++)
                {
                    var h = grid.HexAt(gq, r);
                    if (h == null) continue;
                    var pc = HexCenterOffset(origin, size, sc, r);
                    var col = _featureColors.TryGetValue(h.Terrain, out var vc) ? vc : _featureColors[RegionFeatureType.Barren];
                    drawList.AddNgonFilled(pc, size, ImGui.ColorConvertFloat4ToU32(col), 6);
                    if (size > 4f) drawList.AddNgon(pc, size, hexBorder, 6, 1f);

                    // A LOCATED mineral deposit — "resources HERE" (Industry.HexMinerals). A gold gem + short mineral
                    // name; a mine built here draws its ⚙/token ON TOP (the LOCKED PRINCIPLE — mine sits on the deposit).
                    if (h.DepositMineralId >= 0 && bandSurveyed)
                    {
                        float gr = size * 0.30f; if (gr < 2f) gr = 2f;
                        var gemPos = new Vector2(pc.X, pc.Y - size * 0.32f);
                        drawList.AddNgonFilled(gemPos, gr, ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 0.85f, 0.25f, 1f)), 4);
                        drawList.AddNgon(gemPos, gr, ImGui.ColorConvertFloat4ToU32(new Vector4(0.25f, 0.18f, 0f, 1f)), 4, 1.5f);
                        if (size > 9f && mineralNames.TryGetValue(h.DepositMineralId, out var mn) && !string.IsNullOrEmpty(mn))
                        {
                            string lbl = mn.Length > 4 ? mn.Substring(0, 4) : mn;
                            var tsz = ImGui.CalcTextSize(lbl);
                            drawList.AddText(new Vector2(pc.X - tsz.X * 0.5f, pc.Y + size * 0.10f),
                                ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 0.95f, 0.7f, 1f)), lbl);
                        }
                    }
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

            // 4) click → nearest visible hex. DOUBLE-click zooms into that hex's city (C-track); single-click
            //    selects your units there / marches a selected group there (global A*).
            if (clicked || dbl)
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
                if (bestGq >= 0)
                {
                    if (dbl) { _zoomQ = bestGq; _zoomR = bestR; _status = $"zoomed into hex ({bestGq},{bestR}) — city view"; }
                    else HandleGlobalHexClick(grid, regions, body, myFaction, forcesDB, bestGq, bestR);
                }
            }

            // 5) caption + the H3 range readout for the selected group.
            ImGui.Text($"Surface (globe) — one continuous world, centred on Region {regions[centreBand].Index + 1}; ◀/▶ pans, click a hex to select/march, double-click to zoom into its city.");
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
            _selGQ = gq; _selGR = gr;                 // remember the clicked hex — the target for build-a-mine-on-a-deposit

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
        /// <paramref name="r"/>. Keeps the grid RECTANGULAR (odd rows nudged half a hex), so a wide cylinder window reads
        /// as a proper map — matching the odd-r neighbour model the global pathfinder (G2) uses.</summary>
        private static Vector2 HexCenterOffset(Vector2 origin, float size, int sc, int r)
            => new Vector2(origin.X + size * 1.7320508f * (sc + 0.5f * (r & 1)), origin.Y + size * 1.5f * r);

        // ── C-track: the CITY zoom — the mini-hex grid UNDER one operational hex (Planet → Region → Hex → CityTile) ──
        // Double-clicking a hex on the globe zooms here. Buildings occupy mini-hex tiles 1:1 (the roll-up invariant);
        // "Develop hex" locates the colony's footprint buildings onto this hex and lays them onto tiles. v1 is a VIEW +
        // develop; per-tile placement of new buildings + the area-scaled ("more not bigger") tile count are follow-ups.
        private void DrawCityZoom(SurfaceGrid grid, List<Region> regions, Entity body, int myFaction)
        {
            int rc = Math.Max(1, regions.Count);
            int band = PlanetGridFactory.RegionOfColumn(_zoomQ, grid.Cols, rc);

            if (ImGui.Button("⤢ Back to globe")) { _zoomQ = -1; _zoomR = -1; return; }
            ImGui.SameLine();
            ImGui.Text($"City view — operational hex ({_zoomQ},{_zoomR}), Region {(band < regions.Count ? regions[band].Index + 1 : band + 1)}");
            ImGui.SameLine();
            if (ImGui.Button("Develop hex"))
            {
                var colony = FindMyColony(body, myFaction);
                if (colony != null)
                {
                    GroundBuildings.LocateFootprintsOnGlobalHexes(colony);
                    int laid = CityBuilder.DevelopGlobalHex(body, _zoomQ, _zoomR);
                    _status = laid > 0 ? $"developed hex — {laid} building(s) laid onto mini-hex tiles"
                                       : "nothing new to lay (already developed, or no footprint buildings here)";
                }
                else _status = "you have no colony on this world to develop";
            }

            var hex = CityGridFactory.ResolveGlobalHex(body, _zoomQ, _zoomR);
            var city = CityGridFactory.EnsureCityForGlobalHex(body, _zoomQ, _zoomR);
            if (hex == null || city?.Tiles == null || city.Tiles.Count == 0)
            {
                ImGui.TextDisabled("This hex has no city grid.");
                return;
            }

            var names = GroundBuildings.BuildingNamesOnBody(body);   // engine accessor (ComponentInstancesDB.AllComponents is internal)

            // ── per-tile placement: bring the colony's footprint buildings to THIS hex, arm one, click an empty tile ──
            var placedIds = new HashSet<int>();
            foreach (var t in city.Tiles) if (t.BuildingInstanceId != -1) placedIds.Add(t.BuildingInstanceId);
            var unplaced = new List<int>();
            if (hex.InstallationIds != null)
                foreach (var id in hex.InstallationIds) if (!placedIds.Contains(id)) unplaced.Add(id);

            if (ImGui.Button("Bring buildings here"))
            {
                var colony = FindMyColony(body, myFaction);
                if (colony != null)
                {
                    int n = GroundBuildings.LocateFootprintsOnGlobalHex(colony, _zoomQ, _zoomR);
                    _status = n > 0 ? $"brought {n} building(s) here — pick one and click an empty tile"
                                    : "no un-located footprint buildings to bring (they're placed on another hex already)";
                }
                else _status = "you have no colony on this world";
            }
            int armedId = -1;
            if (unplaced.Count > 0)
            {
                var labels = new string[unplaced.Count + 1];
                labels[0] = "(pick a building to place)";
                for (int i = 0; i < unplaced.Count; i++)
                {
                    string nm = names.TryGetValue(unplaced[i], out var n0) ? n0 : "building #" + unplaced[i];
                    int fp = GroundBuildings.FootprintTilesFor(body, unplaced[i]);
                    labels[i + 1] = fp > 1 ? $"{nm} ({fp} tiles)" : nm;
                }
                if (_placeChoice < 0 || _placeChoice >= labels.Length) _placeChoice = 0;
                ImGui.SameLine();
                ImGui.SetNextItemWidth(240f);
                ImGui.Combo("##placepick", ref _placeChoice, labels, labels.Length);
                if (_placeChoice >= 1 && _placeChoice <= unplaced.Count) armedId = unplaced[_placeChoice - 1];
            }
            else _placeChoice = 0;

            // ── BUILD-here (C3 economy wire): queue a NEW installation through real production, targeted at an empty tile ──
            ComponentDesign armedBuildDesign = null;
            var placeable = PlaceableInstallations(myFaction);
            if (placeable.Count > 0)
            {
                var blabels = new string[placeable.Count + 1];
                blabels[0] = "(build a new installation…)";
                for (int i = 0; i < placeable.Count; i++) blabels[i + 1] = placeable[i].Name;
                if (_buildInstallChoice < 0 || _buildInstallChoice >= blabels.Length) _buildInstallChoice = 0;
                ImGui.SetNextItemWidth(260f);
                ImGui.Combo("##buildinstall", ref _buildInstallChoice, blabels, blabels.Length);
                if (_buildInstallChoice >= 1 && _buildInstallChoice <= placeable.Count) armedBuildDesign = placeable[_buildInstallChoice - 1];
                if (armedBuildDesign != null)
                {
                    ImGui.SameLine();
                    ImGui.TextDisabled("→ click an empty tile to queue the build (materials + time)");
                }
            }
            var reserved = GroundBuild.ReservedTilesOn(body, _zoomQ, _zoomR);   // tiles with a build already queued

            var drawList = ImGui.GetWindowDrawList();
            var canvasPos = ImGui.GetCursorScreenPos();
            var canvasSize = ImGui.GetContentRegionAvail();
            float mapHeight = Math.Max(220f, canvasSize.Y * 0.66f);
            var mapSize = new Vector2(canvasSize.X, mapHeight);
            ImGui.InvisibleButton("citycanvas", mapSize);
            bool clicked = ImGui.IsItemHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
            var mp = ImGui.GetIO().MousePos;

            int R = Math.Max(1, city.Radius);
            float span = 2 * R + 1.6f;
            float size = Math.Max(3f, Math.Min(mapSize.X / (1.7320508f * span), mapSize.Y / (1.5f * span)));
            var center = new Vector2(canvasPos.X + mapSize.X * 0.5f, canvasPos.Y + mapSize.Y * 0.5f);
            uint hexBorder = ImGui.ColorConvertFloat4ToU32(new Vector4(0f, 0f, 0f, 0.30f));

            foreach (var t in city.Tiles)
            {
                var pc = AxialHexCenter(center, size, t.Q, t.R);
                var col = _featureColors.TryGetValue(t.Terrain, out var vc) ? vc : _featureColors[RegionFeatureType.Barren];
                drawList.AddNgonFilled(pc, size, ImGui.ColorConvertFloat4ToU32(col), 6);
                if (size > 4f) drawList.AddNgon(pc, size, hexBorder, 6, 1f);
                if (t.BuildingInstanceId != -1)   // a building occupies this mini-hex
                {
                    float mr = Math.Max(2.5f, size * 0.5f);
                    drawList.AddRectFilled(new Vector2(pc.X - mr, pc.Y - mr), new Vector2(pc.X + mr, pc.Y + mr),
                        ImGui.ColorConvertFloat4ToU32(new Vector4(0.92f, 0.85f, 0.35f, 1f)));
                    drawList.AddRect(new Vector2(pc.X - mr, pc.Y - mr), new Vector2(pc.X + mr, pc.Y + mr),
                        ImGui.ColorConvertFloat4ToU32(new Vector4(0f, 0f, 0f, 0.8f)));
                }
                else if (reserved.Contains((t.Q, t.R)) && size > 3f)   // a build is queued here — under construction
                    drawList.AddNgon(pc, size * 0.6f, ImGui.ColorConvertFloat4ToU32(new Vector4(0.95f, 0.6f, 0.2f, 0.9f)), 6, 2f);
                else if ((armedId >= 0 || armedBuildDesign != null) && size > 4f)   // empty tile + something armed → drop-target hint
                    drawList.AddNgon(pc, size * 0.5f, ImGui.ColorConvertFloat4ToU32(new Vector4(0.9f, 0.9f, 0.4f, 0.5f)), 6, 1f);
            }

            if (clicked)
            {
                CityTile best = null; float bestD = size * 1.2f;
                foreach (var t in city.Tiles)
                {
                    float d = Vector2.Distance(AxialHexCenter(center, size, t.Q, t.R), mp);
                    if (d < bestD) { bestD = d; best = t; }
                }
                if (best != null)
                {
                    if (armedBuildDesign != null && best.BuildingInstanceId == -1)   // queue a NEW build (economy) on this empty tile
                    {
                        var colony = FindMyColony(body, myFaction);
                        if (colony != null && GroundBuild.QueueBuildOnTile(colony, _zoomQ, _zoomR, best.Q, best.R, armedBuildDesign.UniqueID))
                            _status = $"queued {armedBuildDesign.Name} → building on tile ({best.Q},{best.R}) — materials + time; it lands here when done";
                        else
                            _status = colony == null ? "you have no colony on this world to build from"
                                                     : "couldn't build there (tile taken/reserved, or no room for its footprint)";
                    }
                    else if (armedId >= 0 && best.BuildingInstanceId == -1)   // drop an already-built building on this empty tile
                    {
                        if (CityBuilder.PlaceBuildingOnGlobalTile(body, _zoomQ, _zoomR, best.Q, best.R, armedId))
                        {
                            _status = $"placed {(names.TryGetValue(armedId, out var pn) ? pn : "building #" + armedId)} on tile ({best.Q},{best.R})";
                            _placeChoice = 0;
                        }
                        else _status = "couldn't place there";
                    }
                    else if (best.BuildingInstanceId != -1)   // inspect an occupied tile
                        _status = $"tile ({best.Q},{best.R}): {(names.TryGetValue(best.BuildingInstanceId, out var nm) ? nm : "building #" + best.BuildingInstanceId)}";
                    else
                        _status = reserved.Contains((best.Q, best.R)) ? $"tile ({best.Q},{best.R}): reserved (build queued)" : $"tile ({best.Q},{best.R}): empty";
                }
            }

            ImGui.Text($"{city.Tiles.Count} mini-hex tiles — buildings occupy their footprint (▧). \"Build a new installation\" → click an empty tile to queue it through production (⬡ orange = under construction); \"Bring buildings here\" places already-built ones; \"Develop hex\" auto-lays. Click a tile to inspect.");
        }

        /// <summary>Axial hex (q,r) → screen position (pointy-top), centred on <paramref name="origin"/> — for the city
        /// DISK (a hexagon centred at the origin, so axial layout is right; the globe window uses odd-r offset instead).</summary>
        private static Vector2 AxialHexCenter(Vector2 origin, float size, int q, int r)
            => new Vector2(origin.X + size * 1.7320508f * (q + r * 0.5f), origin.Y + size * 1.5f * r);

        /// <summary>The player's colony on this body (owned by <paramref name="myFaction"/>), or null. Defensive.</summary>
        private static Entity FindMyColony(Entity body, int myFaction)
        {
            if (body?.Manager == null) return null;
            foreach (var colony in body.Manager.GetAllEntitiesWithDataBlob<ColonyInfoDB>())
                if (colony.TryGetDataBlob<ColonyInfoDB>(out var ci) && ci.PlanetEntity != null && ci.PlanetEntity.Id == body.Id
                    && colony.FactionOwnerID == myFaction)
                    return colony;
            return null;
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

            // ── Build a MINE on a located deposit hex — the "build a mine ON that deposit" connection ────────
            // The last hex you clicked is the build target; if it holds a mineral deposit, offer to plant a mine
            // ON the ore via PlaceInstallationOnHexOrder (the CI-tested engine path). The mine draws body-wide for
            // now; per-hex depletion (the mine works ITS deposit) is the flagged follow-up.
            try
            {
                var grid = PlanetGridFactory.EnsureGridForBody(body);
                var hex = (grid != null && _selGQ >= 0) ? grid.HexAt(_selGQ, _selGR) : null;
                if (hex != null && hex.DepositMineralId >= 0)
                {
                    var mineDesign = designs.FirstOrDefault(d => d.HasAttribute<Pulsar4X.Industry.MineResourcesAtbDB>());
                    var names = BuildMineralNames();
                    string mineral = names.TryGetValue(hex.DepositMineralId, out var mn) && !string.IsNullOrEmpty(mn)
                        ? mn : $"mineral #{hex.DepositMineralId}";
                    ImGui.Separator();
                    if (mineDesign == null)
                        ImGui.TextDisabled($"◆ {mineral} deposit at ({_selGQ},{_selGR}) — design a Mine to work it.");
                    else if (ImGui.Button($"Build {mineDesign.Name} on this {mineral} deposit ({_selGQ},{_selGR})"))
                    {
                        try
                        {
                            var order = PlaceInstallationOnHexOrder.CreateCommand(colony, _selGQ, _selGR, mineDesign.UniqueID);
                            _uiState.Game.OrderHandler.HandleOrder(order);
                            _status = $"queued {mineDesign.Name} on {mineral} deposit ({_selGQ},{_selGR})";
                        }
                        catch (Exception ex)
                        {
                            _status = "mine-on-hex order failed (logged)";
                            Console.WriteLine($"[RenderError] PlanetViewWindow mine-on-hex order threw: {ex}");
                        }
                    }
                }
            }
            catch { }
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

        // G6 — the DETAILED map legend at the bottom of the globe view (the developer's ask). A collapsible header so it
        // stays out of the way once learned; explains every colour on the map — unit ownership, the unit-type initials,
        // the selection ring, and each terrain colour (grouped, with Ocean flagged impassable) — plus the seam/label cues.
        private void DrawLegend()
        {
            ImGui.Spacing();
            if (!ImGui.CollapsingHeader("Map legend", ImGuiTreeNodeFlags.DefaultOpen)) return;

            int me = _uiState.Faction?.Id ?? -1;

            ImGui.TextDisabled("Units:");
            ImGui.SameLine(); ImGui.TextColored(OwnerColor(me, me), "■ yours");
            ImGui.SameLine(); ImGui.TextColored(new Vector4(0.85f, 0.25f, 0.2f, 1f), "■ hostile");
            ImGui.SameLine(); ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1f), "■ unowned");
            ImGui.SameLine(); ImGui.TextColored(new Vector4(1f, 1f, 0.3f, 1f), "▢ selected group");
            ImGui.TextDisabled("   I = Infantry   A = Armor   R = Artillery      number = units on that hex      » = marching");

            ImGui.Separator();
            ImGui.TextDisabled("Terrain (one continuous world — colours flow across the region seams):");
            LegendRow("Water:  ", (RegionFeatureType.Ocean, "Ocean (impassable)"), (RegionFeatureType.Coast, "Coast"), (RegionFeatureType.Wetland, "Wetland"));
            LegendRow("Lowland:", (RegionFeatureType.Plains, "Plains"), (RegionFeatureType.Forest, "Forest"), (RegionFeatureType.Jungle, "Jungle"), (RegionFeatureType.Desert, "Desert"), (RegionFeatureType.Barren, "Barren"));
            LegendRow("Upland: ", (RegionFeatureType.Highlands, "Highlands"), (RegionFeatureType.Mountains, "Mountains"), (RegionFeatureType.Volcanic, "Volcanic"));
            LegendRow("Cold:   ", (RegionFeatureType.Tundra, "Tundra"), (RegionFeatureType.Ice, "Ice"));
            LegendRow("Gas:    ", (RegionFeatureType.GasLayers, "Gas layers"));

            ImGui.Separator();
            ImGui.TextDisabled("Resources:");
            ImGui.SameLine(); ImGui.TextColored(new Vector4(1f, 0.85f, 0.25f, 1f), "◆ mineral deposit");
            ImGui.SameLine(); ImGui.TextDisabled("(the 4-letter tag names the mineral) — build a mine on the hex to work it.");

            ImGui.Separator();
            ImGui.TextDisabled("A faint yellow vertical line marks a region-band boundary; \"R# (centre)\" labels the band under the middle of the window.");
            ImGui.TextDisabled("◀ / ▶ pans the globe in longitude — it wraps around the far side, so every place is reachable.");
        }

        /// <summary>One labelled row of terrain swatches for the legend (a coloured ■ + name per terrain).</summary>
        private static void LegendRow(string label, params (RegionFeatureType t, string name)[] items)
        {
            ImGui.TextDisabled(label);
            foreach (var (t, name) in items)
            {
                ImGui.SameLine();
                var c = _featureColors.TryGetValue(t, out var vc) ? vc : _featureColors[RegionFeatureType.Barren];
                ImGui.TextColored(c, "■ " + name);
            }
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
