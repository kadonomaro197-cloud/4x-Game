using Newtonsoft.Json;

namespace Pulsar4X.Sites
{
    /// <summary>
    /// Site Engine SE-5a — ONE resolution BRANCH of a field site (docs/SITE-ENGINE-DESIGN.md §3 Branch set / §4).
    /// The whole point of SE-5: a site no longer has a single hard-wired outcome — it offers a SET of honest branches
    /// (fight / contain / study / seal / negotiate), each a different reward <em>or none</em>, and the player commits
    /// one once they understand enough. Branches COMPOSE — a branch is UNLOCKED by accrued knowledge
    /// (<see cref="UnderstandingRequired"/>), never consumed by the others: a patient player who accrues full
    /// understanding still gets to pick seal-vs-ally; an impatient one commits a cheaper branch early for a lesser
    /// outcome. (§4 locked decision: "understand → then choose", never railroaded.)
    ///
    /// This is a plain serializable DATA record (the moddable-catalog shape, like a GroundStanceBlueprint), carried in
    /// <see cref="FieldSiteDB.Branches"/>. SE-5a only ADDS the type + the list (empty on every existing site → the
    /// single-path resolve is untouched → byte-identical); SE-5b teaches the state machine to read it and SE-5c adds
    /// the commit-branch order.
    /// </summary>
    public class SiteBranch
    {
        /// <summary>The player-facing name of this choice (e.g. "Seal the vault", "Ally with the guardian", "Study on").</summary>
        [JsonProperty] public string Name { get; set; } = "Branch";

        /// <summary>How much <see cref="FieldSiteDB.Understanding"/> must be accrued before this branch can be committed
        /// (§4: knowledge unlocks a branch, never a timer). A cheaper branch unlocks earlier for a lesser payoff.</summary>
        [JsonProperty] public double UnderstandingRequired { get; set; } = 100.0;

        /// <summary>Which consumer system this branch pays into when committed (§3 Yield dial) — or <see cref="SiteYield.Nothing"/>
        /// for a branch whose reward is purely narrative/strategic (contain-the-threat with no loot).</summary>
        [JsonProperty] public SiteYield Yield { get; set; } = SiteYield.Nothing;

        /// <summary>Multiplier applied to the site's banked <see cref="FieldSiteDB.Progress"/> when this branch pays out
        /// (1.0 = the full banked magnitude; a hasty branch might pay 0.5, a thorough one 1.0). Not the yield itself —
        /// the magnitude of it.</summary>
        [JsonProperty] public double YieldScale { get; set; } = 1.0;

        /// <summary>The terminal <see cref="SiteStatus"/> committing this branch leaves the site in — Depleted (a one-shot
        /// spent) or Persistent (a standing stream). SE-5b routes the resolve to this instead of the Shape default.</summary>
        [JsonProperty] public SiteStatus ResultStatus { get; set; } = SiteStatus.Depleted;

        /// <summary>True if committing this branch RUPTURES the site into a crisis (the reward carries the risk — §4
        /// ruptured edge). Default false; SE-5d reads it to spawn the crisis site. A benign branch never ruptures.</summary>
        [JsonProperty] public bool Ruptures { get; set; } = false;

        public SiteBranch() { }

        public SiteBranch(SiteBranch other)
        {
            Name = other.Name;
            UnderstandingRequired = other.UnderstandingRequired;
            Yield = other.Yield;
            YieldScale = other.YieldScale;
            ResultStatus = other.ResultStatus;
            Ruptures = other.Ruptures;
        }

        public SiteBranch Clone() => new SiteBranch(this);
    }
}
