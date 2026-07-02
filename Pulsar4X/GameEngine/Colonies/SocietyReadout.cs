using System.Linq;
using System.Text;
using Pulsar4X.Engine;
using Pulsar4X.Names;
using Pulsar4X.Factions;

namespace Pulsar4X.Colonies
{
    /// <summary>
    /// The instrument panel for the M-ECON systems — turns the (otherwise invisible) morale / manpower / economy
    /// / government state into one readable line each, so a live play-test can SEE it move (the Visibility Gate:
    /// you can't tune what you can't read). Pure string-building, no side effects, CI-tested — the client's
    /// "Dump Society" button is a thin caller. Tolerant of missing blobs (an older colony just shows fewer fields).
    /// </summary>
    public static class SocietyReadout
    {
        /// <summary>One line summarising a colony's morale (+ factor breakdown), manpower pools, and tax.</summary>
        public static string Colony(Entity colony)
        {
            var sb = new StringBuilder();
            sb.Append(NameOf(colony));

            long pop = 0;
            if (colony.TryGetDataBlob<ColonyInfoDB>(out var info))
                foreach (var kvp in info.Population) pop += kvp.Value;
            sb.Append($": pop {pop:N0}");

            double morale = ColonyMoraleDB.Neutral;
            if (colony.TryGetDataBlob<ColonyMoraleDB>(out var m))
            {
                morale = m.Morale;
                sb.Append($" | morale {m.Morale:0.0}");
                if (m.Factors.Count > 0)
                    sb.Append(" [" + string.Join(", ", m.Factors
                        .Where(f => f.Key != "baseline")
                        .Select(f => $"{f.Key} {f.Value:+0.0;-0.0;0}")) + "]");
            }

            if (colony.TryGetDataBlob<LegitimacyDB>(out var leg))
            {
                sb.Append($" | legitimacy {leg.Legitimacy:0.0}");
                if (colony.TryGetDataBlob<RebellionDB>(out var reb) && reb.IsRebelling)
                {
                    // Show the reaction-window countdown — the time left to restore legitimacy (or suppress)
                    // before the rebellion resolves (the #38 window the player is racing against).
                    double daysLeft = (reb.ReactionWindowEnds - colony.StarSysDateTime).TotalDays;
                    sb.Append(daysLeft > 0 ? $" !REBELLING ({daysLeft:0}d left)" : " !REBELLING (window lapsed)");
                }
                else if (LegitimacyDB.IsCollapsing(leg.Legitimacy))
                    sb.Append(" (collapsing)");
            }

            // Sustenance (M5b): power/food shortage — 0 until per-capita demand is calibrated. Shown when the blob
            // is present so a colony's brown-out / famine pressure reads at a glance.
            if (colony.TryGetDataBlob<ColonySustenanceDB>(out var sust))
                sb.Append($" | pwr-short {sust.PowerShortage:P0} food-short {sust.FoodShortage:P0}");

            if (colony.TryGetDataBlob<ColonyManpowerDB>(out var mp))
                sb.Append($" | workforce {mp.AvailableBulk(pop):N0}/{ColonyManpowerDB.Workforce(pop):N0}"
                        + $" talent {mp.AvailableTalent(pop):N0}/{ColonyManpowerDB.TalentPool(pop):N0}");

            if (colony.TryGetDataBlob<ColonyEconomyDB>(out var econ))
                sb.Append($" | tax {econ.TaxRate:P0} -> {ColonyEconomyDB.MonthlyTaxIncome(pop, econ.TaxRate, morale):N0}/mo");

            return sb.ToString();
        }

        /// <summary>A faction's government — its classified name + description, or a note if none is set yet.</summary>
        public static string Government(Entity faction)
        {
            if (faction != null && faction.TryGetDataBlob<GovernmentDB>(out var gov))
                return $"{gov.Name()} — {gov.Description()}";
            return "no government set (GovernmentDB not attached yet)";
        }

        /// <summary>
        /// A faction's diplomatic ledger: one line per faction it has met — the derived stance, the relation score,
        /// and any standing treaties (the readout the reactive-drift and treaty systems move). "(has met no one)"
        /// on a fresh single-faction game. The gauge for watching external politics actually change.
        /// </summary>
        public static string Diplomacy(Entity faction)
        {
            if (faction == null || !faction.TryGetDataBlob<Factions.DiplomacyDB>(out var dip))
                return "no diplomacy data";
            if (dip.Relationships.Count == 0)
                return NameOf(faction) + " diplomacy: (has met no one)";

            var game = faction.Manager?.Game;
            var sb = new StringBuilder(NameOf(faction) + " diplomacy:");
            foreach (var rel in dip.Relationships.Values)
            {
                string other = $"faction {rel.OtherFactionId}";
                if (game != null && game.Factions.TryGetValue(rel.OtherFactionId, out var otherFaction))
                    other = NameOf(otherFaction);

                sb.Append($"\n  {other}: {rel.CurrentStance()} ({rel.RelationScore:+0;-0;0})");
                if (rel.AtWar) sb.Append(" [WAR]");
                if (rel.NonAggressionPact) sb.Append(" [NAP]");
                if (rel.TradeAgreement) sb.Append(" [Trade]");
                if (rel.LogisticsAccess) sb.Append(" [Logi]");
                if (rel.MilitaryAccess) sb.Append(" [MilAccess]");
                if (rel.DefensivePact) sb.Append(" [DefPact]");
            }
            return sb.ToString();
        }

        private static string NameOf(Entity e)
        {
            if (e.TryGetDataBlob<NameDB>(out var nameDB))
                return nameDB.GetName(e.FactionOwnerID);
            return $"Entity {e.Id}";
        }
    }
}
