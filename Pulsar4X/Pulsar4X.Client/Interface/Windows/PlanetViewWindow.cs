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

            // ── Controls ────────────────────────────────────────────────────────────────
            if (ImGui.Button("◀ West")) { _centerRegion = left; }
            ImGui.SameLine();
            ImGui.Text($"Region {_centerRegion + 1} of {count}");
            ImGui.SameLine();
            if (ImGui.Button("East ▶")) { _centerRegion = right; }

            int surveyed = regions.Count(r => r.Surveyed);
            ImGui.SameLine();
            ImGui.TextDisabled($"   surveyed {surveyed}/{count}");
            if (!string.IsNullOrEmpty(_status))
            {
                ImGui.SameLine();
                ImGui.TextColored(new Vector4(0.6f, 0.9f, 0.6f, 1f), "  " + _status);
            }
            ImGui.Separator();

            // ── The three-region strip ──────────────────────────────────────────────────
            var drawList = ImGui.GetWindowDrawList();
            var canvasPos = ImGui.GetCursorScreenPos();
            var canvasSize = ImGui.GetContentRegionAvail();
            float mapHeight = Math.Max(150f, canvasSize.Y * 0.58f);
            var mapSize = new Vector2(canvasSize.X, mapHeight);

            ImGui.InvisibleButton("planetcanvas", mapSize);
            bool hovered = ImGui.IsItemHovered();
            bool clicked = hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
            var mp = ImGui.GetIO().MousePos;

            float gap = 8f;
            float colW = (mapSize.X - gap * 2f) / 3f;
            int[] cols = { left, _centerRegion, right };

            _tokenHits.Clear();
            for (int c = 0; c < 3; c++)
            {
                float x0 = canvasPos.X + c * (colW + gap);
                var colMin = new Vector2(x0, canvasPos.Y);
                var colMax = new Vector2(x0 + colW, canvasPos.Y + mapHeight);
                DrawRegionColumn(drawList, colMin, colMax, regions[cols[c]], c == 1, myFaction, forcesDB, envDB);
            }

            // ── Click resolution: a UNIT TOKEN wins over the column background ───────────
            if (clicked)
            {
                bool tokenHit = false;
                foreach (var t in _tokenHits)
                {
                    if (mp.X >= t.min.X && mp.X <= t.max.X && mp.Y >= t.min.Y && mp.Y <= t.max.Y)
                    {
                        // Toggle selection of this group (only YOUR units are orderable).
                        if (HasSelection && _selRegion == t.region && _selFaction == t.faction && _selType == t.type)
                            ClearSelection();
                        else { _selRegion = t.region; _selFaction = t.faction; _selType = t.type; _status = ""; }
                        tokenHit = true;
                        break;
                    }
                }

                if (!tokenHit)
                {
                    // Column background: MOVE if a group is selected and this column is an adjacent region; else rotate.
                    for (int c = 0; c < 3; c++)
                    {
                        float x0 = canvasPos.X + c * (colW + gap);
                        if (mp.X < x0 || mp.X > x0 + colW) continue;
                        int target = cols[c];
                        if (HasSelection && _selFaction == myFaction && target != _selRegion
                            && _selRegion < regions.Count && regions[_selRegion].Neighbors.Contains(target))
                            MarchSelectedTo(regions, target);
                        else
                            _centerRegion = target;   // navigate
                        break;
                    }
                }
            }

            // ── Below the canvas: selection actions, build panel, region detail ─────────
            ImGui.Separator();
            DrawSelectionBar(regions, forcesDB, myFaction, left, right);
            DrawBuildPanel(body, myFaction);
            DrawFormationPanel(body, forcesDB, myFaction, regions, left, right);
            ImGui.Separator();
            DrawRegionDetail(regions[_centerRegion], forcesDB, envDB);
            DrawLegend();
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
                }
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
