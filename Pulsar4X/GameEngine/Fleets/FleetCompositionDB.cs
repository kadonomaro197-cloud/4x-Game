using Newtonsoft.Json;
using Pulsar4X.Datablobs;

namespace Pulsar4X.Fleets
{
    /// <summary>
    /// Marks a fleet the AI is GROWING toward a composition (slice 2), and remembers WHICH ladder it's climbing plus
    /// whether it has crossed the deploy floor yet. The passive twin of <see cref="FleetRoleDB"/>: no processor is
    /// keyed to it — it's a memo the fleet-assembly policy reads to (a) find an under-strength FORMING fleet to grow
    /// before starting a new one, and (b) fire the deploy transition exactly once.
    ///
    /// The three thresholds mirror <see cref="FleetCompositionTemplate"/> so a faction's own ladder (the slice-3
    /// per-faction override) rides on the fleet across save/load — the template itself is a plain (unserialised) class,
    /// so we store its NUMBERS here rather than a reference. The current <see cref="FleetCompositionTier"/> is COMPUTED
    /// from the fleet's live ship count via <see cref="Template"/>, never stored (it changes as ships join/die).
    ///
    /// A hand-made player fleet never carries this blob — so it's how the AI tells its own forming strike fleets apart
    /// from a player's fleets (and from the auto-formed role sub-fleets, which carry <see cref="FleetRoleDB"/> instead).
    /// The class name is LOCKED (L3: <c>TypeNameHandling.Objects</c> embeds it in saves).
    /// </summary>
    public class FleetCompositionDB : BaseDataBlob
    {
        /// <summary>The ladder's name (e.g. "Strike Fleet") — carried so a per-faction template survives save/load.</summary>
        [JsonProperty] public string TemplateName { get; internal set; } = "Strike Fleet";

        /// <summary>Fewest ships before the fleet is worth deploying (below it → Forming).</summary>
        [JsonProperty] public int MinToDeploy { get; internal set; } = 3;

        /// <summary>The size the AI grows the fleet to when resources allow (reorganise into role sub-fleets).</summary>
        [JsonProperty] public int IdealSize { get; internal set; } = 8;

        /// <summary>The size the AI grows the fleet to only when resources are plentiful / the situation demands it.</summary>
        [JsonProperty] public int PerfectSize { get; internal set; } = 18;

        /// <summary>Latched TRUE the first time the fleet crosses <see cref="MinToDeploy"/> — so the deploy transition
        /// (and, slice 3, the standing patrol order) fires ONCE, not every monthly cycle.</summary>
        [JsonProperty] public bool Deployed { get; internal set; }

        public FleetCompositionDB() { }

        public FleetCompositionDB(FleetCompositionTemplate template)
        {
            TemplateName = template.Name;
            MinToDeploy  = template.MinToDeploy;
            IdealSize    = template.IdealSize;
            PerfectSize  = template.PerfectSize;
        }

        /// <summary>Rebuild the (unserialised) <see cref="FleetCompositionTemplate"/> from the stored numbers, so the
        /// tier read (<see cref="FleetCompositionTemplate.TierFor"/>) uses the same ladder that was assigned.</summary>
        public FleetCompositionTemplate Template =>
            new FleetCompositionTemplate(TemplateName, MinToDeploy, IdealSize, PerfectSize);

        // A REAL clone that copies every field (deliberately NOT FleetDB.Clone()'s blank-return, which would drop the memo).
        public override object Clone() => new FleetCompositionDB
        {
            TemplateName = TemplateName,
            MinToDeploy  = MinToDeploy,
            IdealSize    = IdealSize,
            PerfectSize  = PerfectSize,
            Deployed     = Deployed,
        };
    }
}
