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

        private static string NameOf(Entity e)
        {
            if (e.TryGetDataBlob<NameDB>(out var nameDB))
                return nameDB.GetName(e.FactionOwnerID);
            return $"Entity {e.Id}";
        }
    }
}
