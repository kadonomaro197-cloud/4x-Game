using Newtonsoft.Json;
using Pulsar4X.Datablobs;

namespace Pulsar4X.Sites
{
    /// <summary>The site's lifecycle state — the §4 state machine of docs/SITE-ENGINE-DESIGN.md.
    /// DISCOVERED → (work) → WORKED → (enough understanding + a committed branch) → RESOLVE → one of the terminal
    /// states DEPLETED / PERSISTENT / RUPTURED. No timers: an unresolved incident/persistent site applies pressure
    /// and the player chooses WHEN to act.</summary>
    public enum SiteStatus
    {
        /// <summary>Known to exist, not yet worked.</summary>
        Discovered,
        /// <summary>A worker is on-site; progress + understanding accrue.</summary>
        Worked,
        /// <summary>Resolved as a one-shot — the site is spent.</summary>
        Depleted,
        /// <summary>Resolved as a standing stream — it keeps producing.</summary>
        Persistent,
        /// <summary>The persistent faucet ruptured into a new crisis site (the reward carried the risk). SE-5.</summary>
        Ruptured
    }

    /// <summary>Which Command-Berth ROLE (and leader type) can work the site, and which pillar its output feeds
    /// (§3 Role dial). SE-1 uses Science.</summary>
    public enum SiteRole { Science, Tactical, Diplomatic, Intelligence, Engineering }

    /// <summary>The goal framing (§3 Shape dial): fill-a-bar (one-shot depletes) vs. hold-a-faucet (persistent
    /// stream) vs. stop-the-bleed (incident — bleeds you until resolved, SE-4).</summary>
    public enum SiteShape { OneShot, Persistent, Incident }

    /// <summary>The twist that makes it an episode, not a vending machine (§3 Hook dial). SE-1 uses Benign
    /// (a benign anomaly that CAN later rupture — that edge is SE-5).</summary>
    public enum SiteHook { Benign, Guardian, Cursed, Contested, Gated, Reactive, MoralFork }

    /// <summary>Which consumer system the payoff routes into (§3 Yield dial). SE-1 uses Research.</summary>
    public enum SiteYield { Nothing, Research, Blueprint, Resource, Population, Leader, StrategicAsset, NetworkRoute }

    /// <summary>
    /// Site Engine SE-1a — the SITE RECORD, the heart of the one engine every mid-game episode is a row in
    /// (docs/SITE-ENGINE-DESIGN.md). A located thing → a berth-seated leader works it → it resolves down a branch →
    /// a yield. This blob carries the §3 dials (Role/Shape/Hook/Yield) plus the live §4 state-machine fields (status +
    /// accrued Progress and Understanding). The pure transitions live in <see cref="SiteMachine"/>.
    ///
    /// This slice is the DATA MODEL + the state machine only — a new blob NOT yet attached to any entity, NOT yet
    /// driven by a processor, so it is byte-identical (no site exists until SE-1b's factory creates one). SE-1b adds
    /// the anomaly-site entity + presence detection + the processor; SE-1c delivers the research yield on resolve.
    /// </summary>
    public class FieldSiteDB : BaseDataBlob
    {
        // ---- The §3 dials (authoring knobs; the runtime is fixed) ----
        [JsonProperty] public SiteRole Role { get; set; } = SiteRole.Science;
        [JsonProperty] public SiteShape Shape { get; set; } = SiteShape.OneShot;
        [JsonProperty] public SiteHook Hook { get; set; } = SiteHook.Benign;
        [JsonProperty] public SiteYield Yield { get; set; } = SiteYield.Research;

        // ---- The live §4 state ----
        [JsonProperty] public SiteStatus Status { get; set; } = SiteStatus.Discovered;

        /// <summary>Work accrued by the on-site worker — the magnitude of the yield banked so far (delivered on resolve).</summary>
        [JsonProperty] public double Progress { get; set; }

        /// <summary>Knowledge accrued — the gate that UNLOCKS the resolve branches (§4: knowledge unlocks, never a timer).</summary>
        [JsonProperty] public double Understanding { get; set; }

        /// <summary>How much understanding a branch needs before it can be committed. A patient player accrues the full
        /// amount for the best branch; an impatient one can act early on partial knowledge for a lesser outcome (later).</summary>
        [JsonProperty] public double UnderstandingToResolve { get; set; } = 100.0;

        /// <summary>Guard so a one-shot yield is delivered exactly once (SE-1c reads/sets it at resolve).</summary>
        [JsonProperty] public bool YieldDelivered { get; set; }

        /// <summary>Which faction's on-site worker is banking this site's progress (SE-1b: the last worker seen
        /// present). The yield routes to this faction (SE-1c). -1 = nobody has worked it yet.</summary>
        [JsonProperty] public int WorkedByFactionId { get; set; } = -1;

        // ---- SE-3a: the SURFACE location (a site on a planet's ground, not a point in space) ----
        // A space anomaly locates itself by a co-blob PositionDB; a surface site instead lives on a specific body's
        // region/hex — the same way a GroundUnit locates itself (RegionIndex + GlobalQ/GlobalR). Location-neutral
        // defaults (-1) mean "not a surface site", so the space-anomaly path is byte-identical.

        /// <summary>The planet body entity this surface site sits on, or -1 for a space anomaly. Set → this is a
        /// surface site (see <see cref="IsSurfaceSite"/>) worked by a ground unit standing on it (SE-3b), not a ship.</summary>
        [JsonProperty] public int SurfaceBodyEntityId { get; set; } = -1;

        /// <summary>The body region the site sits in (-1 = none). Ground combat's `Region.OwnerFactionID` on this
        /// region is the guardian gate (SE-3d).</summary>
        [JsonProperty] public int SurfaceRegionIndex { get; set; } = -1;

        /// <summary>The site's exact hex on the body's continuous cylinder grid (global axial Q/R; -1 = unplaced) —
        /// where a worker unit must stand and where the ruin is located.</summary>
        [JsonProperty] public int SurfaceGlobalQ { get; set; } = -1;
        [JsonProperty] public int SurfaceGlobalR { get; set; } = -1;

        /// <summary>True when this record carries a surface location (a planet-ground site), false for a space anomaly.</summary>
        [JsonIgnore] public bool IsSurfaceSite => SurfaceBodyEntityId >= 0;

        public FieldSiteDB() { }

        public FieldSiteDB(FieldSiteDB other)
        {
            Role = other.Role;
            Shape = other.Shape;
            Hook = other.Hook;
            Yield = other.Yield;
            Status = other.Status;
            Progress = other.Progress;
            Understanding = other.Understanding;
            UnderstandingToResolve = other.UnderstandingToResolve;
            YieldDelivered = other.YieldDelivered;
            WorkedByFactionId = other.WorkedByFactionId;
            SurfaceBodyEntityId = other.SurfaceBodyEntityId;
            SurfaceRegionIndex = other.SurfaceRegionIndex;
            SurfaceGlobalQ = other.SurfaceGlobalQ;
            SurfaceGlobalR = other.SurfaceGlobalR;
        }

        public override object Clone() => new FieldSiteDB(this);
    }
}
