namespace Pulsar4X.Factions
{
    /// <summary>
    /// F-C2 (docs/GOVERNMENT-AND-POLITICS-DESIGN.md — the popular-demands pillar): the FIXED set of interest blocs
    /// (the "Stellaris-parties"). A small, closed vocabulary; their SUPPORT is emergent (derived from the sim each
    /// cycle, like legitimacy — never a parallel stored system), and each is the source of the demands the demand
    /// engine (F-C2b) surfaces.
    /// </summary>
    public enum PoliticalBloc
    {
        /// <summary>Jobs, low unemployment, housing.</summary>
        Labor,
        /// <summary>Low tax, trade, growth.</summary>
        Merchants,
        /// <summary>Military spending, war, confront rivals.</summary>
        Militarists,
        /// <summary>Openness, civil liberty, less authority.</summary>
        Liberty,
        /// <summary>Stability, authority, tradition.</summary>
        Order,
    }

    /// <summary>
    /// F-C2a (docs/AI-BRAIN-BUILD-TRACKER.md): the bloc substrate — which blocs a regime makes LOUD. Pure/derived,
    /// no stored state (support is computed, not a parallel system). The government dials bias which blocs organise
    /// (design §Blocs): the Militarism dial amplifies Militarists, Openness amplifies Liberty, Authority amplifies
    /// Order. This is the substrate the demand engine (F-C2b) weights its emergent demands by; nothing consumes it
    /// yet, so it is byte-identical.
    /// </summary>
    public static class PoliticalBlocs
    {
        /// <summary>The closed set of blocs — iterate this rather than hard-coding the five.</summary>
        public static readonly PoliticalBloc[] All =
        {
            PoliticalBloc.Labor, PoliticalBloc.Merchants, PoliticalBloc.Militarists,
            PoliticalBloc.Liberty, PoliticalBloc.Order,
        };

        /// <summary>
        /// How LOUD a bloc is under a regime — 1.0 baseline; a favouring dial at High amplifies its bloc, at Low damps
        /// it. Only the three dial-linked blocs move (Militarists↔Militarism, Liberty↔Openness, Order↔Authority);
        /// Labor and Merchants aren't dial-biased in v1 (they organise around ECONOMIC state — unemployment, tax —
        /// which the demand engine reads directly, F-C2b). Null government → all baseline.
        /// </summary>
        public static double Loudness(GovernmentDB government, PoliticalBloc bloc)
        {
            if (government == null)
                return 1.0;

            return bloc switch
            {
                PoliticalBloc.Militarists => NotchFactor(government.Militarism),
                PoliticalBloc.Liberty => NotchFactor(government.Openness),
                PoliticalBloc.Order => NotchFactor(government.Authority),
                _ => 1.0,
            };
        }

        /// <summary>A regime dial's amplification of the bloc it favours: Low 0.5 · Mid 1.0 · High 1.5.</summary>
        public static double NotchFactor(GovNotch notch) => notch switch
        {
            GovNotch.Low => 0.5,
            GovNotch.High => 1.5,
            _ => 1.0,
        };
    }
}
