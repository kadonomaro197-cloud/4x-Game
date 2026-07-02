using Newtonsoft.Json;
using System.Collections.Generic;
using Pulsar4X.Datablobs;
using Pulsar4X.Colonies;

namespace Pulsar4X.Factions
{
    /// <summary>One notch of a government dial. Each dial has three settings.</summary>
    public enum GovNotch { Low, Mid, High }

    /// <summary>How a population vents discontent under this regime (a government rule-override).</summary>
    public enum DiscontentResponse { Emigration, Unrest }

    /// <summary>
    /// The empire-wide regime as a MODULATOR (docs/GOVERNMENT-AND-POLITICS-DESIGN.md): a panel of four dials,
    /// three notches each, that re-skin gameplay via coefficient overrides AND rule overrides — NOT just
    /// numbers. The dials set the values the rest of the engine reads (e.g. <see cref="CrewPolicy"/> is the rule
    /// the M3-2 construction gate flips; <see cref="TaxCeiling"/> caps the M4 tax lever).
    ///
    /// This is the substrate (the dials + the derivation + the live classifier). Wiring each processor to read
    /// it is a later step; the design is "build the dials, ship the menu" — a named government is just a saved
    /// dial-setting (see <see cref="Name"/>).
    ///
    /// Dial orientation (Low → High): AUTHORITY People → One Ruler; ECONOMY Free Market → Command;
    /// OPENNESS Closed → Open; MILITARISM Pacifist → Militarist.
    /// </summary>
    public class GovernmentDB : BaseDataBlob
    {
        [JsonProperty] public GovNotch Authority { get; set; } = GovNotch.Mid;
        [JsonProperty] public GovNotch Economy { get; set; } = GovNotch.Mid;
        [JsonProperty] public GovNotch Openness { get; set; } = GovNotch.Mid;
        [JsonProperty] public GovNotch Militarism { get; set; } = GovNotch.Mid;

        public GovernmentDB() { }

        public GovernmentDB(GovNotch authority, GovNotch economy, GovNotch openness, GovNotch militarism)
        {
            Authority = authority;
            Economy = economy;
            Openness = openness;
            Militarism = militarism;
        }

        public GovernmentDB(GovernmentDB other)
        {
            Authority = other.Authority;
            Economy = other.Economy;
            Openness = other.Openness;
            Militarism = other.Militarism;
        }

        public override object Clone() => new GovernmentDB(this);

        // --- RULE overrides (a different law, not a different number) ---

        /// <summary>The crew-shortage rule (M3-2): a high-authority regime conscripts (build understaffed);
        /// otherwise the build blocks. This is the developer's dictatorship example.</summary>
        public CrewShortagePolicy CrewPolicy()
            => Authority == GovNotch.High ? CrewShortagePolicy.BuildUnderstaffed : CrewShortagePolicy.Block;

        /// <summary>How discontent vents: closed/authoritarian regimes convert emigration into unrest.</summary>
        public DiscontentResponse Discontent()
            => (Authority == GovNotch.High || Openness == GovNotch.Low) ? DiscontentResponse.Unrest : DiscontentResponse.Emigration;

        // --- COEFFICIENT overrides (scalar dials on existing levers) ---

        /// <summary>Max tax rate the regime tolerates (M4): authority raises the ceiling.</summary>
        public double TaxCeiling() => Authority switch { GovNotch.Low => 0.3, GovNotch.High => 0.9, _ => 0.5 };

        /// <summary>How strongly morale/public opinion pulls (both ways): the People-end amplifies it.</summary>
        public double MoraleWeight() => Authority switch { GovNotch.Low => 1.5, GovNotch.High => 0.5, _ => 1.0 };

        /// <summary>Research output multiplier: an open society races ahead, a closed one drags.</summary>
        public double ResearchMultiplier() => Openness switch { GovNotch.Low => 0.75, GovNotch.High => 1.25, _ => 1.0 };

        /// <summary>Military build speed/cost multiplier: command economy + militarism builds fast and cheap.</summary>
        public double MilitaryBuildMultiplier()
        {
            double m = 1.0;
            if (Economy == GovNotch.High) m *= 1.3; else if (Economy == GovNotch.Low) m *= 0.9;
            if (Militarism == GovNotch.High) m *= 1.2; else if (Militarism == GovNotch.Low) m *= 0.85;
            return m;
        }

        /// <summary>Sign/scale of war's effect on morale: militarists take pride (+), pacifists tire of it (−).</summary>
        public double WarMoraleFactor() => Militarism switch { GovNotch.Low => -1.0, GovNotch.High => 0.5, _ => -0.25 };

        // --- The live classifier (name + description for the panel) ---

        private static readonly Dictionary<(GovNotch, GovNotch, GovNotch, GovNotch), string> IconicNames = new()
        {
            { (GovNotch.Low,  GovNotch.Low,  GovNotch.High, GovNotch.Low),  "Liberal Democracy" },
            { (GovNotch.Low,  GovNotch.Low,  GovNotch.High, GovNotch.High), "Martial Republic" },
            { (GovNotch.Low,  GovNotch.High, GovNotch.High, GovNotch.Low),  "Democratic Socialist Union" },
            { (GovNotch.High, GovNotch.High, GovNotch.Low,  GovNotch.Low),  "Totalitarian State" },
            { (GovNotch.High, GovNotch.High, GovNotch.Low,  GovNotch.High), "Totalitarian War-State" },
            { (GovNotch.High, GovNotch.Low,  GovNotch.High, GovNotch.Low),  "Corporate Plutocracy" },
            { (GovNotch.High, GovNotch.High, GovNotch.Mid,  GovNotch.High), "Military Junta" },
            { (GovNotch.Mid,  GovNotch.Mid,  GovNotch.Mid,  GovNotch.Mid),  "Federal Republic" },
        };

        /// <summary>The government's name: an iconic name for recognised dial combos, else a generated title.</summary>
        public string Name()
        {
            if (IconicNames.TryGetValue((Authority, Economy, Openness, Militarism), out var iconic))
                return iconic;
            // Generated fallback so every combo names itself.
            return $"{AuthorityAdj(true)} {EconomyAdj(true)} State";
        }

        /// <summary>A plain-language description assembled from the dials — always non-empty for any combo.</summary>
        public string Description()
            => $"a {OpennessAdj()}, {EconomyAdj(false)}, {MilitarismAdj()} {AuthorityAdj(false)} state";

        private string AuthorityAdj(bool title) => Authority switch
        {
            GovNotch.Low => title ? "Egalitarian" : "egalitarian",
            GovNotch.High => title ? "Authoritarian" : "authoritarian",
            _ => title ? "Representative" : "representative"
        };
        private string EconomyAdj(bool title) => Economy switch
        {
            GovNotch.Low => title ? "Free-Market" : "free-market",
            GovNotch.High => title ? "Command" : "command",
            _ => title ? "Mixed-Economy" : "mixed-economy"
        };
        private string OpennessAdj() => Openness switch { GovNotch.Low => "closed", GovNotch.High => "open", _ => "guarded" };
        private string MilitarismAdj() => Militarism switch { GovNotch.Low => "pacifist", GovNotch.High => "militarist", _ => "balanced" };
    }
}
