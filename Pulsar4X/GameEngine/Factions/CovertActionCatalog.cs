namespace Pulsar4X.Factions
{
    /// <summary>
    /// The covert operations an agent can run against a rival (docs/ESPIONAGE-AND-INTELLIGENCE-DESIGN.md §E-catalog).
    /// Data-driven like <see cref="ExchangeCatalog"/> — the catalog IS the list of what the spy game can do.
    /// </summary>
    public enum CovertAction
    {
        /// <summary>Raise the intel level on a facet — the cheap, low-risk baseline.</summary>
        GatherIntel,
        /// <summary>Steal a technology from their labs.</summary>
        StealTech,
        /// <summary>Siphon funds from their treasury.</summary>
        StealFunds,
        /// <summary>Sabotage a military asset or installation.</summary>
        Sabotage,
        /// <summary>Foment unrest in a wavering province — nudges it toward rebellion/defection (ties to F-C2).</summary>
        SowUnrest,
        /// <summary>Turn or eliminate one of their officials — the highest-risk play.</summary>
        TurnOrAssassinate,
        /// <summary>Feed them FALSE intel about your intent or strength.</summary>
        Disinformation,
        /// <summary>Defensive — hunt enemy agents on your own soil.</summary>
        CounterIntel,
    }

    /// <summary>One covert action's static definition: which intel facet it bears on + its baseline detection risk.</summary>
    public readonly struct CovertActionDef
    {
        public readonly CovertAction Action;
        /// <summary>The intel facet this action primarily bears on (what it reads/attacks).</summary>
        public readonly IntelFacet TargetsFacet;
        /// <summary>Baseline chance (0..1) the op is detected before skill/counter-intel — a flagged balance dial.</summary>
        public readonly double BaseDetectionRisk;
        public readonly string Description;

        public CovertActionDef(CovertAction action, IntelFacet targetsFacet, double baseDetectionRisk, string description)
        {
            Action = action;
            TargetsFacet = targetsFacet;
            BaseDetectionRisk = baseDetectionRisk;
            Description = description;
        }
    }

    /// <summary>
    /// F-C3c (docs/ESPIONAGE-AND-INTELLIGENCE-DESIGN.md): the COVERT-ACTION CATALOG — the broad, data-driven menu of
    /// what an agent can do, mirroring <see cref="ExchangeCatalog"/>. One <see cref="CovertActionDef"/> per action,
    /// carrying the facet it bears on and its baseline detection risk (louder ops are riskier: gather is cheap,
    /// turn/assassinate is deadly). Pure data → byte-identical (nothing tasks an agent yet; the agent-as-component +
    /// Spymaster delegate that consume this are the follow-on). The RISK resolution is F-C3d.
    /// </summary>
    public static class CovertActionCatalog
    {
        public static readonly CovertActionDef[] All =
        {
            new CovertActionDef(CovertAction.GatherIntel,       IntelFacet.Disposition,      0.10, "Raise the intel level on a facet — the cheap, low-risk baseline."),
            new CovertActionDef(CovertAction.StealTech,         IntelFacet.Secrets,          0.50, "Steal a technology from their labs."),
            new CovertActionDef(CovertAction.StealFunds,        IntelFacet.Economy,          0.40, "Siphon funds from their treasury."),
            new CovertActionDef(CovertAction.Sabotage,          IntelFacet.Military,         0.60, "Sabotage a military asset or installation."),
            new CovertActionDef(CovertAction.SowUnrest,         IntelFacet.InternalPolitics, 0.45, "Foment unrest in a wavering province — nudges it toward rebellion/defection."),
            new CovertActionDef(CovertAction.TurnOrAssassinate, IntelFacet.InternalPolitics, 0.80, "Turn or eliminate one of their officials — the highest-risk play."),
            new CovertActionDef(CovertAction.Disinformation,    IntelFacet.Disposition,      0.35, "Feed them FALSE intel about your intent or strength."),
            new CovertActionDef(CovertAction.CounterIntel,      IntelFacet.Secrets,          0.05, "Defensive — hunt enemy agents on your own soil."),
        };

        /// <summary>The definition for an action. Every enum value has exactly one def (guarded by the catalog gauge).</summary>
        public static CovertActionDef ByAction(CovertAction action)
        {
            foreach (var def in All)
                if (def.Action == action)
                    return def;
            return All[0]; // unreachable — the catalog is complete
        }
    }
}
