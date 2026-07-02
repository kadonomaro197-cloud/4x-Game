using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ImGuiNET;
using Pulsar4X.Engine;
using Pulsar4X.Datablobs;
using Pulsar4X.Extensions;
using Pulsar4X.Colonies;
using Pulsar4X.Factions;
using Pulsar4X.Industry;
using Pulsar4X.People;
using Pulsar4X.Storage;
using Pulsar4X.Technology;
using Pulsar4X.Galaxy;

namespace Pulsar4X.Client
{
    public static class EntityDisplay
    {
        public static void DisplaySummary(this Entity entity, EntityState entityState, GlobalUIState uiState)
        {
            Vector2 windowContentSize = ImGui.GetContentRegionAvail();
            var firstChildSize = new Vector2(windowContentSize.X * 0.33f, windowContentSize.Y);
            var secondChildSize = new Vector2(windowContentSize.X * 0.33f, windowContentSize.Y);
            var thirdChildSize = new Vector2(windowContentSize.X * 0.33f - (windowContentSize.X * 0.01f), windowContentSize.Y);
            if(ImGui.BeginChild("ColonySummary1", firstChildSize, ImGuiChildFlags.Borders))
            {
                var colonyInfoDb = entity.GetDataBlob<ColonyInfoDB>();
                var bodyInfoDb = colonyInfoDb.PlanetEntity.GetDataBlob<SystemBodyInfoDB>();
                var headerName = colonyInfoDb.PlanetEntity.GetDefaultName() + " Information";

                if(ImGui.CollapsingHeader(headerName, ImGuiTreeNodeFlags.DefaultOpen))
                {
                    ImGui.Columns(2);
                    ImGui.PushStyleColor(ImGuiCol.Text, Styles.DescriptiveColor);
                    ImGui.Text("Name");
                    ImGui.PopStyleColor();
                    ImGui.NextColumn();
                    if(ImGui.SmallButton(colonyInfoDb.PlanetEntity.GetDefaultName()))
                    {
                        uiState.EntityClicked(colonyInfoDb.PlanetEntity.Id, uiState.SelectedStarSystemId, MouseButtons.Primary);
                    }
                    ImGui.NextColumn();
                    ImGui.Separator();
                    DisplayHelpers.PrintRow("Type", bodyInfoDb.BodyType.ToDescription());
                    DisplayHelpers.PrintRow("Tectonic Activity", bodyInfoDb.Tectonics.ToDescription());
                    DisplayHelpers.PrintRow("Gravity", Stringify.Velocity(bodyInfoDb.Gravity));
                    DisplayHelpers.PrintRow("Temperature", bodyInfoDb.BaseTemperature.ToString("#.#") + " C");
                    DisplayHelpers.PrintRow("Length of Day", bodyInfoDb.LengthOfDay.TotalHours + " hours");
                    DisplayHelpers.PrintRow("Tilt", bodyInfoDb.AxialTilt.ToString("#") + "°");
                    DisplayHelpers.PrintRow("Magnetic Field", bodyInfoDb.MagneticField.ToString("#") + " μT");
                    DisplayHelpers.PrintRow("Radiation Level", bodyInfoDb.RadiationLevel.ToString("#"));
                    DisplayHelpers.PrintRow("Atmospheric Dust", bodyInfoDb.AtmosphericDust.ToString("#"), separator: false);
                }
                ImGui.Columns(1);
                if(colonyInfoDb.PlanetEntity.TryGetDataBlob<AtmosphereDB>(out var atmosphereDB))
                {
                    atmosphereDB.Display(entityState, uiState);
                }
                else
                {
                    if(ImGui.CollapsingHeader("Atmosphere", ImGuiTreeNodeFlags.DefaultOpen))
                    {
                        ImGui.Text("No Atmosphere");
                    }
                }
            }
            ImGui.EndChild();

            ImGui.SameLine();
            if(ImGui.BeginChild("ColonySummary2", secondChildSize, ImGuiChildFlags.Borders))
            {
                entity.GetDataBlob<ColonyInfoDB>().Display(entityState, uiState);
                ImGui.Columns(1);

                if(entity.TryGetDataBlob<InfrastructureDB>(out var infrastructure)
                    && ImGui.CollapsingHeader("Infrastructure", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    bool overCapacity = infrastructure.CapacityAvailable < 0;

                    ImGui.Columns(2);
                    DisplayHelpers.PrintRow("Provided", infrastructure.CapacityProvided.ToString("N0"));
                    DisplayHelpers.PrintRow("Used", infrastructure.CapacityRequired.ToString("N0"));
                    DisplayHelpers.PrintRow("Available", infrastructure.CapacityAvailable.ToString("N0"));
                    ImGui.Columns(1);

                    // Use TextUnformatted: ImGui.Text/TextColored treat the string as a printf
                    // format, so a literal '%' would be parsed as a format specifier.
                    if(overCapacity)
                    {
                        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.4f, 0.4f, 1f));
                        ImGui.TextUnformatted($"Over capacity - all output reduced to {infrastructure.Efficiency * 100:0}%");
                        ImGui.PopStyleColor();
                    }
                    else
                    {
                        ImGui.PushStyleColor(ImGuiCol.Text, Styles.DescriptiveColor);
                        ImGui.TextUnformatted($"Output at {infrastructure.Efficiency * 100:0}% of capacity");
                        ImGui.PopStyleColor();
                    }
                }

                if(ImGui.CollapsingHeader("Installations", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    if(entity.TryGetDataBlob<ComponentInstancesDB>(out var componentInstances))
                    {
                        componentInstances.Display(entityState, uiState);
                    }
                }
            }
            ImGui.EndChild();

            ImGui.SameLine();
            if(ImGui.BeginChild("ColonySummary3", thirdChildSize, ImGuiChildFlags.Borders))
            {
                if(ImGui.CollapsingHeader("Stockpile", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    if(entity.TryGetDataBlob<CargoStorageDB>(out var storage))
                    {
                        var size = ImGui.GetContentRegionAvail();
                        ImGui.PushStyleColor(ImGuiCol.Button, Styles.Theme.Button.ToImVector4());
                        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Styles.Theme.ButtonHovered.ToImVector4());
                        ImGui.PushStyleColor(ImGuiCol.ButtonActive, Styles.Theme.ButtonActive.ToImVector4());
                        if(ImGui.Button("Initiate Transfer", new Vector2(size.X - 8, 18)))
                        {
                            CreateTransferWindow.GetInstance().SetLeft(entity);
                            CreateTransferWindow.GetInstance().SetActive(true);
                        }
                        ImGui.PopStyleColor(3);

                        ImGui.Columns(2);
                        DisplayHelpers.PrintRow("Total Mass in Storage", Stringify.Mass(storage.TotalStoredMass));
                        DisplayHelpers.PrintRow("Transfer Rate", storage.TransferRate.ToString() + " kg/hr");
                        DisplayHelpers.PrintRow("Transfer Range", storage.TransferRangeDv_mps.ToString("0.#") + " dV m/s", tooltipOne: "This is confusing as hell :D", separator: false);
                        ImGui.Columns(1);
                        storage.Display(entityState, uiState, ImGuiTreeNodeFlags.None);
                    }
                }
            }
            ImGui.EndChild();
        }

        // ── Society tab ────────────────────────────────────────────────────────────────────────────────
        // The player-facing instrument panel for the M-ECON / political state of ONE colony — morale (+ the factor
        // breakdown that explains WHY), legitimacy (+ rebellion countdown), the manpower pools, power/food
        // sustenance, tax→income, and the governing regime that modulates them all. Until now these numbers were
        // reachable ONLY via DevTools "Dump Society" → a log line in SM mode; a player couldn't see them to make a
        // decision. Everything here READS the same public blobs the CI-tested SocietyReadout formats (no new engine
        // math), so it's a thin, defensive draw: each section is guarded by TryGetDataBlob (a colony missing a blob
        // just omits that section) and every read is a public property/method. Values are colour-banded so the
        // player reads "this colony is in trouble" at a glance.
        public static void DisplaySociety(this Entity entity, EntityState entityState, GlobalUIState uiState)
        {
            if(!entity.TryGetDataBlob<ColonyInfoDB>(out var info))
            {
                ImGui.TextUnformatted("No colony data.");
                return;
            }

            long pop = info.Population.Values.Sum();
            double morale = ColonyMoraleDB.Neutral;

            // ── Morale & legitimacy — the loyalty gauges ──
            if(ImGui.CollapsingHeader("Morale & Legitimacy", ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.Columns(2);
                if(entity.TryGetDataBlob<ColonyMoraleDB>(out var m))
                {
                    morale = m.Morale;
                    SocietyRow("Morale", $"{m.Morale:0.0} / 100", Band0to100(m.Morale),
                        "0–100, 50 = neutral. Below 50 people emigrate; above 50 they immigrate.");

                    // The factor breakdown is the WHY behind the number — the lever a player acts on.
                    foreach(var f in m.Factors.Where(f => f.Key != "baseline"))
                        SocietyRow("  · " + f.Key, f.Value.ToString("+0.0;-0.0;0"),
                            f.Value >= 0 ? Styles.GoodColor : Styles.BadColor);
                }
                else SocietyRow("Morale", "n/a", Styles.NeutralColor);

                if(entity.TryGetDataBlob<LegitimacyDB>(out var leg))
                {
                    SocietyRow("Legitimacy", $"{leg.Legitimacy:0.0} / 100", Band0to100(leg.Legitimacy),
                        "The regime's hold on this province. Below 20 it collapses into rebellion.");

                    if(entity.TryGetDataBlob<RebellionDB>(out var reb) && reb.IsRebelling)
                    {
                        double daysLeft = (reb.ReactionWindowEnds - uiState.Game.TimePulse.GameGlobalDateTime).TotalDays;
                        SocietyRow("  · Status", daysLeft > 0 ? $"REBELLING — {daysLeft:0} days to act" : "REBELLING — window lapsed",
                            Styles.TerribleColor, "Restore legitimacy (ease tax / overcrowding) before the window closes.");
                    }
                    else if(LegitimacyDB.IsCollapsing(leg.Legitimacy))
                        SocietyRow("  · Status", "COLLAPSING", Styles.BadColor);
                }
                ImGui.Columns(1);
            }

            // ── People — the finite manpower the colony draws crew/talent from ──
            if(entity.TryGetDataBlob<ColonyManpowerDB>(out var mp)
                && ImGui.CollapsingHeader("People (manpower)", ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.Columns(2);
                long availBulk = mp.AvailableBulk(pop),  totalBulk = ColonyManpowerDB.Workforce(pop);
                long availTal  = mp.AvailableTalent(pop), totalTal  = ColonyManpowerDB.TalentPool(pop);
                SocietyRow("Workforce (crew/labour)", $"{availBulk:N0} free of {totalBulk:N0}",
                    availBulk > 0 ? Styles.NeutralColor : Styles.BadColor,
                    "Bulk manpower. A ship build blocks (or conscripts) when this hits 0.");
                SocietyRow("Talent (officers/scientists)", $"{availTal:N0} free of {totalTal:N0}",
                    availTal > 0 ? Styles.NeutralColor : Styles.BadColor, separator: false);
                ImGui.Columns(1);
            }

            // ── Sustenance — brown-out / famine pressure (0% until per-capita demand is calibrated) ──
            if(entity.TryGetDataBlob<ColonySustenanceDB>(out var sust)
                && ImGui.CollapsingHeader("Sustenance (power / food)", ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.Columns(2);
                SocietyRow("Power shortage", sust.PowerShortage.ToString("P0"), ShortageColor(sust.PowerShortage),
                    "Fraction of power demand unmet. Sours morale; zero = fully powered.");
                SocietyRow("Food shortage", sust.FoodShortage.ToString("P0"), ShortageColor(sust.FoodShortage),
                    "Fraction of food demand unmet. Severe shortage starves population.", separator: false);
                ImGui.Columns(1);
            }

            // ── Economy — the tax lever (which itself feeds back into morale) ──
            if(entity.TryGetDataBlob<ColonyEconomyDB>(out var econ)
                && ImGui.CollapsingHeader("Economy (tax)", ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.Columns(2);
                SocietyRow("Tax rate", econ.TaxRate.ToString("P0"),
                    econ.TaxRate <= 0.25 ? Styles.GoodColor : econ.TaxRate <= 0.5 ? Styles.MediocreColor : Styles.BadColor,
                    "Higher tax earns more but lowers morale.");
                SocietyRow("Monthly income", $"{ColonyEconomyDB.MonthlyTaxIncome(pop, econ.TaxRate, morale):N0} / mo",
                    Styles.NeutralColor, "Scales with population AND morale — a happy colony pays more.", separator: false);
                ImGui.Columns(1);
            }

            // ── Government — the empire regime that modulates all of the above (tax ceiling, morale weight, ...) ──
            if(uiState.Game != null && uiState.Game.Factions.TryGetValue(entity.FactionOwnerID, out var owner)
                && owner.TryGetDataBlob<GovernmentDB>(out var gov)
                && ImGui.CollapsingHeader("Government", ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.PushStyleColor(ImGuiCol.Text, Styles.HighlightColor);
                ImGui.TextUnformatted(gov.Name());
                ImGui.PopStyleColor();
                ImGui.PushStyleColor(ImGuiCol.Text, Styles.DescriptiveColor);
                ImGui.TextWrapped(gov.Description());
                ImGui.PopStyleColor();
            }
        }

        // One label→value row with a colour-banded value. TextUnformatted on the value so a literal '%' (from a
        // P0-formatted percentage) isn't parsed as a printf specifier (the same trap DisplaySummary avoids). Assumes
        // ImGui.Columns(2) is active — matches DisplayHelpers.PrintRow's contract.
        private static void SocietyRow(string label, string value, Vector4 valueColor, string? tooltip = null, bool separator = true)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, Styles.DescriptiveColor);
            ImGui.TextUnformatted(label);
            ImGui.PopStyleColor();
            if(tooltip != null && ImGui.IsItemHovered()) ImGui.SetTooltip(tooltip);
            ImGui.NextColumn();
            ImGui.PushStyleColor(ImGuiCol.Text, valueColor);
            ImGui.TextUnformatted(value);
            ImGui.PopStyleColor();
            ImGui.NextColumn();
            if(separator) ImGui.Separator();
        }

        // 0–100 gauge colour band (morale, legitimacy): green good → red terrible.
        private static Vector4 Band0to100(double v) =>
            v >= 65 ? Styles.GoodColor :
            v >= 50 ? Styles.OkColor :
            v >= 35 ? Styles.MediocreColor :
            v >= 20 ? Styles.BadColor :
                      Styles.TerribleColor;

        // Shortage fraction colour band (0 = fine/green, 1 = total/red).
        private static Vector4 ShortageColor(double s) =>
            s <= 0.001 ? Styles.GoodColor :
            s < 0.25   ? Styles.OkColor :
            s < 0.5    ? Styles.MediocreColor :
            s < 0.75   ? Styles.BadColor :
                         Styles.TerribleColor;

        public static void DisplayIndustry(this Entity entity, EntityState entityState, GlobalUIState uiState)
        {
            IndustryDisplay.GetInstance(entityState).Display(uiState);
        }

        public static void DisplayConstruction(this Entity entity, EntityState entityState, GlobalUIState uiState)
        {
            ConstructionDisplay.GetInstance(entityState).Display(uiState);
        }
        public static void DisplayMining(this Entity entity, GlobalUIState uiState)
        {
            if(uiState.Faction == null) return;

            var mineralStaticInfo = uiState.Faction.GetDataBlob<FactionInfoDB>().Data.CargoGoods.GetMineralsList();
            var minerals = entity.GetDataBlob<ColonyInfoDB>().PlanetEntity.HasDataBlob<MineralsDB>() ?
                            entity.GetDataBlob<ColonyInfoDB>().PlanetEntity.GetDataBlob<MineralsDB>()?.Minerals :
                            null;
            var miningRates = entity.HasDataBlob<MiningDB>() ? entity.GetDataBlob<MiningDB>().ActualMiningRate : new ();
            var storage = entity.GetDataBlob<CargoStorageDB>()?.TypeStores;

            Vector2 topSize = ImGui.GetContentRegionAvail();
            if(ImGui.BeginChild("NumberOfMines" + entity.Id, new Vector2(topSize.X, 28f), ImGuiChildFlags.Borders, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
            {
                if(entity.TryGetDataBlob<MiningDB>(out var miningDB))
                {
                    ImGui.Text("Number of Mines:");
                    if(ImGui.IsItemHovered())
                        ImGui.SetTooltip("You can build more mines on this colony using the Production tab.");
                    ImGui.SameLine();
                    ImGui.PushStyleColor(ImGuiCol.Text, Styles.HighlightColor);
                    ImGui.Text(miningDB.NumberOfMines.ToString());
                    ImGui.PopStyleColor();

                }
                else
                {
                    ImGui.Text("Number of Mines: 0");
                }
            }
            ImGui.EndChild();

            if(ImGui.BeginTable("###MineralTable" + entity.Id, 6, ImGuiTableFlags.BordersV | ImGuiTableFlags.BordersOuterH | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp))
            {
                ImGui.TableSetupColumn("Mineral");
                ImGui.TableSetupColumn("Stockpile");
                ImGui.TableSetupColumn("Available to Mine");
                ImGui.TableSetupColumn("Accessibility");
                ImGui.TableSetupColumn("Annual Production");
                ImGui.TableSetupColumn("Years to Depletion");
                ImGui.TableHeadersRow();

                if(minerals == null) minerals = new Dictionary<int, MineralDeposit>();

                foreach(var (id, mineral) in minerals)
                {
                    var mineralData = mineralStaticInfo.FirstOrDefault(x => x.ID == id);

                    if(mineralData == null) continue;

                    var stockpileData = storage?.FirstOrDefault(x => x.Value.CurrentStoreInUnits.ContainsKey(id)).Value;
                    var stockpileUnits = stockpileData?.CurrentStoreInUnits;
                    var annualProduction = miningRates.ContainsKey(id) ? 365 * miningRates[id] : 0;

                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.Text(mineralData.Name);
                    if(ImGui.IsItemHovered())
                        DisplayHelpers.DescriptiveTooltip(mineralData.Name, "Mineral", mineralData.Description);
                    ImGui.TableNextColumn();
                    if(stockpileUnits != null && stockpileUnits.ContainsKey(id))
                    {
                        ImGui.Text(stockpileUnits[id].ToString("#,###,###,###,###,###,##0"));
                    }
                    else
                    {
                        if(storage == null)
                            ImGui.Text("Unavailable");
                        else
                            ImGui.Text("0");
                    }
                    if(ImGui.IsItemHovered())
                        ImGui.SetTooltip("Amount of " + mineralData.Name + " available for use in the colony stockpile.");

                    ImGui.TableNextColumn();
                    ImGui.Text(mineral.Amount.For(uiState.FactionMask)?.ToString("#,###,###,###,###,###,##0") ?? "N/A");
                    if(ImGui.IsItemHovered())
                        ImGui.SetTooltip("Amount of " + mineralData.Name + " available that can be mined from this colony.");
                    ImGui.TableNextColumn();
                    ImGui.Text(mineral.Accessibility.ToString("0.00"));
                    if(ImGui.IsItemHovered())
                        ImGui.SetTooltip("How easy it is to mine " + mineralData.Name + " from this colony.\n\n1.0 = easiest\n0.0 = hardest");
                    ImGui.TableNextColumn();
                    if(miningRates.ContainsKey(id))
                    {
                        ImGui.Text(annualProduction.ToString("#,###,###"));
                        if(ImGui.IsItemHovered())
                            ImGui.SetTooltip("Annual production of " + mineralData.Name + " from this colony.");
                    }
                    else
                    {
                        ImGui.Text("-");
                        if(ImGui.IsItemHovered())
                            ImGui.SetTooltip("This colony is currently unable to mine " + mineralData.Name + ".");
                    }
                    ImGui.TableNextColumn();
                    if(annualProduction > 0)
                    {
                        var amount = mineral.Amount.For(uiState.FactionMask) ?? 0;
                        string yearsToDepletion = Math.Round((double)amount / (double)annualProduction, 4).ToString("#.0");
                        ImGui.Text(yearsToDepletion);
                        if(ImGui.IsItemHovered())
                            ImGui.SetTooltip("The colony will exhaust the available " + mineralData.Name + " in " + yearsToDepletion + " years.");
                    }
                    else
                    {
                        ImGui.Text("-");
                    }
                }

                ImGui.EndTable();

                if(minerals.Count == 0)
                {
                    ImGui.Text("No minerals available.");
                }
            }
        }

        public static void DisplayResearch(this Entity entity, EntityState entityState, GlobalUIState uiState)
        {
            if(!entity.TryGetDataBlob<EntityResearchDB>(out var researchDB)) return;

            Vector2 topSize = ImGui.GetContentRegionAvail();
            if(ImGui.BeginChild("NumberOfResearchLabs" + entity.Id, new Vector2(topSize.X, 28f), ImGuiChildFlags.Borders, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
            {
                ImGui.Text("Universities:");
                ImGui.SameLine();
                ImGui.PushStyleColor(ImGuiCol.Text, Styles.HighlightColor);
                ImGui.Text(researchDB.Labs.Count.ToString("0"));
                ImGui.PopStyleColor();
                ImGui.SameLine();
                ImGui.Text("Research Points:");
                ImGui.SameLine();
                ImGui.PushStyleColor(ImGuiCol.Text, Styles.HighlightColor);
                ImGui.Text(researchDB.Labs.Values.Sum().ToString());
                ImGui.PopStyleColor();

                ImGui.EndChild();
            }

            Vector2 sizeAvailable = ImGui.GetContentRegionAvail();
            if(ImGui.BeginChild("UniversityList", sizeAvailable, ImGuiChildFlags.Borders))
            {
                foreach(var (instance, value) in researchDB.Labs)
                {
                    ImGui.Text(instance.Name);
                    ImGui.Text(value.ToString());
                }
                ImGui.EndChild();
            }
        }

        public static void DisplayLogistics(this Entity entity, EntityState entityState, GlobalUIState uiState)
        {
            ColonyLogisticsDisplay.GetInstance(entity.GetFactionOwner.GetDataBlob<FactionInfoDB>().Data, entityState).Display();
        }

        public static void DisplayNavalAcademy(this Entity entity, EntityState entityState, GlobalUIState uiState)
        {
            if(!entity.TryGetDataBlob<NavalAcademyDB>(out var navalAcademyDB)) return;

            Vector2 topSize = ImGui.GetContentRegionAvail();
            if(ImGui.BeginChild("NumberOfAcademies" + entity.Id, new Vector2(topSize.X, 28f), ImGuiChildFlags.Borders, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
            {
                ImGui.Text("Academies:");
                ImGui.SameLine();
                ImGui.PushStyleColor(ImGuiCol.Text, Styles.HighlightColor);
                ImGui.Text(navalAcademyDB.Academies.Count.ToString("0"));
                ImGui.PopStyleColor();
                ImGui.EndChild();
            }

            Vector2 sizeAvailable = ImGui.GetContentRegionAvail();
            if(ImGui.BeginChild("AcademyList", new Vector2(sizeAvailable.X * .25f, sizeAvailable.Y), ImGuiChildFlags.Borders))
            {
                if(ImGui.BeginTable("AcademyListTable", 4, Styles.TableFlags))
                {
                    ImGui.TableSetupColumn("#", ImGuiTableColumnFlags.None, 0.1f);
                    ImGui.TableSetupColumn("Class Size", ImGuiTableColumnFlags.None, 0.25f);
                    ImGui.TableSetupColumn("Length", ImGuiTableColumnFlags.None, 0.2f);
                    ImGui.TableSetupColumn("Graduation", ImGuiTableColumnFlags.None, 0.3f);
                    ImGui.TableHeadersRow();

                    for(int i = 0; i < navalAcademyDB.Academies.Count; i++)
                    {
                        ImGui.TableNextColumn();
                        ImGui.Text((i + 1).ToString());
                        ImGui.TableNextColumn();
                        ImGui.Text(navalAcademyDB.Academies[i].ClassSize.ToString());
                        ImGui.TableNextColumn();
                        ImGui.Text(navalAcademyDB.Academies[i].TrainingPeriodInMonths.ToString() + " months");
                        ImGui.TableNextColumn();
                        ImGui.Text(navalAcademyDB.Academies[i].GraduationDate.ToShortDateString());
                    }
                    ImGui.EndTable();
                }
                ImGui.EndChild();
            }
        }
    }
}
