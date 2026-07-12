using System.Collections.Generic;

namespace Pulsar4X.Factions
{
    /// <summary>What a bloc is asking for. Flavor text dresses it; the substance is generated (design §Demands).</summary>
    public enum DemandKind
    {
        LowerTaxes,
        CreateJobs,
        ImproveLiving,
        EndTheWar,
        ConfrontRival,
    }

    /// <summary>
    /// One emergent demand: which bloc voices it, the ask, and how hard they're pushing (bigger = louder/angrier).
    /// A demand = {source bloc, the ask, a satisfaction condition} — the condition is the ask's inverse (F-C2c wires
    /// enact/refuse). Value type; cheap to surface a fresh list each cycle.
    /// </summary>
    public readonly struct PoliticalDemand
    {
        public readonly PoliticalBloc Bloc;
        public readonly DemandKind Kind;
        public readonly double Pressure;

        public PoliticalDemand(PoliticalBloc bloc, DemandKind kind, double pressure)
        {
            Bloc = bloc;
            Kind = kind;
            Pressure = pressure;
        }
    }

    /// <summary>
    /// F-C2b (docs/GOVERNMENT-AND-POLITICS-DESIGN.md §Demands): the demand engine. Emergent, not scripted — it reads
    /// the SAME per-factor breakdown the morale system already computes (<see cref="Pulsar4X.Colonies.ColonyMoraleDB"/>
    /// <c>Factors</c>: tax/employment/conditions/crowding/…) and surfaces a demand wherever a factor is bad enough
    /// that a bloc organises around it. "A demand is essentially a morale factor bad enough that a bloc forms." War
    /// adds a political demand whose flavour flips on the regime's militarism. Pressure scales with how bad the
    /// factor is × how LOUD the bloc is under this regime (F-C2a). Pure/derived → byte-identical (nothing consumes
    /// the list yet; the enact/refuse teeth + legitimacy consequence are F-C2c).
    /// </summary>
    public static class DemandEngine
    {
        /// <summary>A morale factor must be at least this negative before a bloc organises a demand around it.</summary>
        public const double DemandThreshold = -10.0;

        /// <summary>
        /// Surface the demands emerging from a province's morale <paramref name="moraleFactors"/> (the same breakdown
        /// the morale system computes), the regime's <paramref name="government"/> lean, and whether it's
        /// <paramref name="atWar"/>. Returns a fresh list each call.
        /// </summary>
        public static List<PoliticalDemand> SurfaceDemands(
            IReadOnlyDictionary<string, double> moraleFactors, GovernmentDB government, bool atWar)
        {
            var demands = new List<PoliticalDemand>();

            if (moraleFactors != null)
            {
                // Economic pressures → the bloc that owns that grievance.
                AddIfBad(demands, moraleFactors, "tax", PoliticalBloc.Merchants, DemandKind.LowerTaxes, government);
                AddIfBad(demands, moraleFactors, "employment", PoliticalBloc.Labor, DemandKind.CreateJobs, government);
                AddIfBad(demands, moraleFactors, "conditions", PoliticalBloc.Labor, DemandKind.ImproveLiving, government);
                AddIfBad(demands, moraleFactors, "crowding", PoliticalBloc.Labor, DemandKind.ImproveLiving, government);
            }

            // War: a hawkish regime's Militarists demand you press the fight; otherwise the public wants it ended.
            if (atWar)
            {
                if (government != null && government.Militarism == GovNotch.High)
                    demands.Add(new PoliticalDemand(PoliticalBloc.Militarists, DemandKind.ConfrontRival,
                        PoliticalBlocs.Loudness(government, PoliticalBloc.Militarists)));
                else
                    demands.Add(new PoliticalDemand(PoliticalBloc.Liberty, DemandKind.EndTheWar,
                        PoliticalBlocs.Loudness(government, PoliticalBloc.Liberty)));
            }

            return demands;
        }

        private static void AddIfBad(List<PoliticalDemand> demands, IReadOnlyDictionary<string, double> factors,
            string factorKey, PoliticalBloc bloc, DemandKind kind, GovernmentDB government)
        {
            if (factors.TryGetValue(factorKey, out double delta) && delta <= DemandThreshold)
            {
                // Worse factor + louder bloc = stronger demand. (delta is negative → -delta is the magnitude.)
                double pressure = -delta * PoliticalBlocs.Loudness(government, bloc);
                demands.Add(new PoliticalDemand(bloc, kind, pressure));
            }
        }
    }
}
