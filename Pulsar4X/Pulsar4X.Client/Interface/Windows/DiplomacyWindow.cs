using System;
using System.Collections.Generic;
using System.Numerics;
using ImGuiNET;
using Pulsar4X.Client.Interface.Widgets;
using Pulsar4X.Factions;
using Pulsar4X.Names;

namespace Pulsar4X.Client
{
    /// <summary>
    /// The player-facing DIPLOMACY readout — your standing with every faction you've met, reachable from the toolbar
    /// (alongside Colony Management + the distance ruler). Until now the relationship ledger was only in DevTools →
    /// "Dump Society" (a log line, SM-only); this is the in-game panel. Reads the player faction's
    /// <see cref="DiplomacyDB"/> directly (public getters, the same data the CI-tested <c>SocietyReadout.Diplomacy</c>
    /// formats) — a thin, read-only, defensive draw. One row per met faction: stance (colour-banded), relation score,
    /// and standing treaties. Mirrors the standard PulsarGuiWindow pattern (GetInstance + Display).
    /// </summary>
    public class DiplomacyWindow : PulsarGuiWindow
    {
        private bool _errorLogged;

        private DiplomacyWindow()
        {
            _flags = ImGuiWindowFlags.None;
        }

        internal static DiplomacyWindow GetInstance()
        {
            if (!_uiState.LoadedWindows.ContainsKey(typeof(DiplomacyWindow)))
                return new DiplomacyWindow();
            return (DiplomacyWindow)_uiState.LoadedWindows[typeof(DiplomacyWindow)];
        }

        internal override void Display()
        {
            if (!IsActive) return;

            // Body wrapped so a throw can't skip Window.End() (the 2026-07-02 colony-window cascade lesson): End()
            // is unconditional; a body fault is caught + logged once and the window still closes cleanly.
            if (Window.Begin("Diplomacy", ref IsActive))
            {
                try { RenderBody(); }
                catch (Exception e)
                {
                    ImGui.TextUnformatted("Diplomacy view hit an error (logged).");
                    if (!_errorLogged)
                    {
                        Console.WriteLine("[RenderError] DiplomacyWindow body threw (logged once): " + e);
                        Console.Out.Flush();
                        _errorLogged = true;
                    }
                }
            }
            Window.End();
        }

        private void RenderBody()
        {
            var player = _uiState.PlayerFaction;
            if (player == null || !player.TryGetDataBlob<DiplomacyDB>(out var dip))
            {
                ImGui.TextDisabled("No diplomacy data for your faction.");
                return;
            }

            ImGui.TextDisabled("Your standing with every faction you've met. War/Hostile fight on sight; treaties gate trade, access, and defence.");
            ImGui.Separator();

            if (dip.Relationships.Count == 0)
            {
                ImGui.TextDisabled("You have not met any other factions yet.");
                ImGui.TextDisabled("(First contact happens when your sensors first detect a foreign faction's ship.)");
                return;
            }

            if (ImGui.BeginTable("diplomacyledger", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
            {
                ImGui.TableSetupColumn("Faction", ImGuiTableColumnFlags.WidthStretch, 0.35f);
                ImGui.TableSetupColumn("Stance", ImGuiTableColumnFlags.WidthStretch, 0.2f);
                ImGui.TableSetupColumn("Score", ImGuiTableColumnFlags.WidthStretch, 0.15f);
                ImGui.TableSetupColumn("Treaties", ImGuiTableColumnFlags.WidthStretch, 0.3f);
                ImGui.TableHeadersRow();

                foreach (var rel in dip.Relationships.Values)
                {
                    string name = "Faction " + rel.OtherFactionId;
                    if (_uiState.Game != null && _uiState.Game.Factions.TryGetValue(rel.OtherFactionId, out var other)
                        && other.TryGetDataBlob<NameDB>(out var nameDB))
                        name = nameDB.OwnersName;

                    var stance = rel.CurrentStance();

                    ImGui.TableNextRow();
                    ImGui.TableSetColumnIndex(0); ImGui.TextUnformatted(name);
                    ImGui.TableSetColumnIndex(1);
                    ImGui.PushStyleColor(ImGuiCol.Text, StanceColor(stance));
                    ImGui.TextUnformatted(stance.ToString());
                    ImGui.PopStyleColor();
                    ImGui.TableSetColumnIndex(2); ImGui.TextUnformatted(rel.RelationScore.ToString("+0;-0;0"));
                    ImGui.TableSetColumnIndex(3); ImGui.TextUnformatted(TreatyList(rel));
                }
                ImGui.EndTable();
            }
        }

        private static string TreatyList(RelationshipState r)
        {
            var t = new List<string>();
            if (r.AtWar) t.Add("WAR");
            if (r.NonAggressionPact) t.Add("Non-Aggression");
            if (r.TradeAgreement) t.Add("Trade");
            if (r.LogisticsAccess) t.Add("Logistics");
            if (r.MilitaryAccess) t.Add("Mil. Access");
            if (r.DefensivePact) t.Add("Defensive Pact");
            return t.Count == 0 ? "—" : string.Join(", ", t);
        }

        // Colour band for the headline stance: War/Hostile red, Neutral grey, Friendly/Allied green.
        private static Vector4 StanceColor(DiplomaticStance s) => s switch
        {
            DiplomaticStance.War      => Styles.TerribleColor,
            DiplomaticStance.Hostile  => Styles.BadColor,
            DiplomaticStance.Neutral  => Styles.NeutralColor,
            DiplomaticStance.Friendly => Styles.GoodColor,
            DiplomaticStance.Allied   => Styles.HighlightColor,
            _                         => Styles.NeutralColor
        };
    }
}
