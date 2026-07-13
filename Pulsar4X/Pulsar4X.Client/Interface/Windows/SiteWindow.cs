using System;
using ImGuiNET;
using Pulsar4X.Client.Interface.Widgets;
using Pulsar4X.Engine;
using Pulsar4X.Names;
using Pulsar4X.Sites;

namespace Pulsar4X.Client
{
    /// <summary>
    /// Site Engine SE-6b — the player-facing SITE panel. A window onto the field sites you've discovered (anomalies /
    /// ruins / outbreaks): each site's progress (work banked + understanding accrued), its status, who's working it, and
    /// — for a site that offers a CHOICE — buttons to COMMIT a resolution branch. This is the piece that makes the SE-5
    /// branch decision real for a player: a branched site waits at "Worked" until you pick a branch here.
    ///
    /// A thin, defensive draw over the CI-tested engine (the client is runtime-CI-blind, so the discipline is: read
    /// public getters, act through CI-tested paths, never hard-index). It reads public getters on <see cref="FieldSiteDB"/>
    /// / <see cref="SiteMachine"/> and issues the CI-tested <see cref="CommitSiteBranchOrder"/> through the normal order
    /// path (<c>Game.OrderHandler.HandleOrder</c>). Mirrors <see cref="DiplomacyWindow"/> verbatim (GetInstance + Display
    /// + Window.Begin/End with a try/catch body so a throw can't skip End()). Reachable from the toolbar.
    /// </summary>
    public class SiteWindow : PulsarGuiWindow
    {
        private bool _errorLogged;

        private SiteWindow()
        {
            _flags = ImGuiWindowFlags.None;
        }

        internal static SiteWindow GetInstance()
        {
            if (!_uiState.LoadedWindows.ContainsKey(typeof(SiteWindow)))
                return new SiteWindow();
            return (SiteWindow)_uiState.LoadedWindows[typeof(SiteWindow)];
        }

        internal override void Display()
        {
            if (!IsActive) return;

            // Body wrapped so a throw can't skip Window.End() (the colony-window cascade lesson): End() is
            // unconditional; a body fault is caught + logged once and the window still closes cleanly.
            if (Window.Begin("Field Sites", ref IsActive))
            {
                try { RenderBody(); }
                catch (Exception e)
                {
                    ImGui.TextUnformatted("Site view hit an error (logged).");
                    if (!_errorLogged)
                    {
                        Console.WriteLine("[RenderError] SiteWindow body threw (logged once): " + e);
                        Console.Out.Flush();
                        _errorLogged = true;
                    }
                }
            }
            Window.End();
        }

        private void RenderBody()
        {
            var game = _uiState.Game;
            var player = _uiState.PlayerFaction;
            if (game == null || player == null)
            {
                ImGui.TextDisabled("No game / faction loaded.");
                return;
            }

            ImGui.TextDisabled("Anomalies, ruins, and outbreaks you've found. Work a site (park a ship on a space anomaly,");
            ImGui.TextDisabled("or hold its ground with a unit) to bank progress; a site that offers a choice lets you pick how it resolves.");
            ImGui.Separator();

            int shown = 0;
            foreach (var (_, sysState) in _uiState.StarSystemStates)
            {
                var system = sysState?.StarSystem;
                if (system == null) continue;

                foreach (var siteEntity in system.GetAllEntitiesWithDataBlob<FieldSiteDB>())
                {
                    if (!siteEntity.TryGetDataBlob<FieldSiteDB>(out var db)) continue;
                    RenderSite(game, player, siteEntity, db);
                    shown++;
                }
            }

            if (shown == 0)
            {
                ImGui.TextDisabled("No field sites discovered yet.");
                ImGui.TextDisabled("(Enable DevTools \"Auto-spawn home-world demo sites on New Game\" to seed a couple.)");
            }
        }

        private void RenderSite(Game game, Entity player, Entity siteEntity, FieldSiteDB db)
        {
            string name = siteEntity.TryGetDataBlob<NameDB>(out var nameDB) ? nameDB.OwnersName : ("Site " + siteEntity.Id);
            string where = db.IsSurfaceSite ? "surface" : "space";

            if (!ImGui.CollapsingHeader($"{name}  ({where}) - {db.Status}##site{siteEntity.Id}", ImGuiTreeNodeFlags.DefaultOpen))
                return;

            ImGui.Indent();

            ImGui.TextUnformatted($"Role: {db.Role}    Shape: {db.Shape}    Yield: {db.Yield}");
            ImGui.TextUnformatted($"Work banked: {db.Progress:0}    Understanding: {db.Understanding:0}");

            string worker = db.WorkedByFactionId < 0
                ? "nobody yet"
                : (game.Factions.TryGetValue(db.WorkedByFactionId, out var wf) && wf.TryGetDataBlob<NameDB>(out var wn)
                    ? wn.OwnersName
                    : ("faction " + db.WorkedByFactionId));
            ImGui.TextUnformatted($"Worked by: {worker}");

            bool playerWorked = db.WorkedByFactionId == player.Id;
            bool resolved = db.Status != SiteStatus.Discovered && db.Status != SiteStatus.Worked;

            if (db.HasBranches)
            {
                ImGui.Spacing();
                ImGui.TextDisabled("Choose how to resolve it (each option unlocks as you understand more):");

                for (int i = 0; i < db.Branches.Count; i++)
                {
                    var branch = db.Branches[i];
                    bool unlocked = SiteMachine.IsBranchUnlocked(db, i);
                    string label = $"{branch.Name}  -  needs {branch.UnderstandingRequired:0} understanding, yields {branch.Yield}";

                    if (resolved && db.CommittedBranchIndex == i)
                    {
                        ImGui.PushStyleColor(ImGuiCol.Text, Styles.GoodColor);
                        ImGui.TextUnformatted("[committed] " + label);
                        ImGui.PopStyleColor();
                    }
                    else if (!resolved && unlocked && playerWorked)
                    {
                        if (ImGui.Button($"Commit: {branch.Name}##commit{siteEntity.Id}_{i}"))
                            Commit(game, siteEntity, i, branch.Name);
                    }
                    else
                    {
                        string why = resolved ? "(the site is already resolved)"
                            : !unlocked ? "(locked - keep working the site to understand it)"
                            : !playerWorked ? "(your faction hasn't worked this site)"
                            : "";
                        ImGui.TextDisabled(label + "   " + why);
                    }
                }
            }
            else
            {
                ImGui.TextDisabled(resolved ? "Resolved." : "Single-path: resolves automatically once it's understood.");
            }

            ImGui.Unindent();
            ImGui.Separator();
        }

        private void Commit(Game game, Entity siteEntity, int branchIndex, string branchName)
        {
            try
            {
                game.OrderHandler.HandleOrder(CommitSiteBranchOrder.CreateCommand(siteEntity, branchIndex));
                Console.WriteLine($"[site] committed branch '{branchName}' on site #{siteEntity.Id}");
                Console.Out.Flush();
            }
            catch (Exception e)
            {
                Console.WriteLine("[site] commit failed: " + e);
                Console.Out.Flush();
            }
        }
    }
}
