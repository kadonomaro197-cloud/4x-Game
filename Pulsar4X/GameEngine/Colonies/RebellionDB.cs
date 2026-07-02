using Newtonsoft.Json;
using System;
using Pulsar4X.Datablobs;

namespace Pulsar4X.Colonies
{
    /// <summary>
    /// A province's REBELLION state — the grave rung of internal politics made into a PROCESS you can fight, not an
    /// instant loss (docs/GOVERNMENT-AND-POLITICS-DESIGN.md "What 'the system rebels' ACTUALLY does", locked #38).
    /// When a province's <see cref="LegitimacyDB"/> falls into the collapse band it does NOT immediately flip owner
    /// — it enters a REBELLION with a **reaction window**: a span during which you can respond (pour in legitimacy —
    /// enact demands, ship aid, replace the governor — OR militarily suppress it, which IS the ground-combat MVP
    /// pointed inward). Restore legitimacy in time and the rebellion is quelled; let the window lapse and it
    /// resolves (secession / defection — the NEXT slices).
    ///
    /// Driven by <see cref="LegitimacyProcessor"/> (which already runs per province and knows the legitimacy): it
    /// BEGINS a rebellion when legitimacy drops below <see cref="LegitimacyDB.CollapseThreshold"/> and QUELLS it when
    /// legitimacy climbs back to <see cref="RecoveryThreshold"/> (hysteresis above the collapse line, so it can't
    /// flicker on the boundary). Attached to every province at factory time (default: not rebelling), so no risky
    /// dynamic blob-adds in the hotloop; a colony/station with no active rebellion just carries an idle blob.
    ///
    /// v1 scope: the rebellion STATE + the reaction window + begin/quell. The window-EXPIRY resolution
    /// (secession-to-a-new-faction, then espionage-driven defection) and the suppress-it-with-ground-troops wire are
    /// the follow-on #38 slices. So this changes no ownership yet — it lights up the collapse hook and starts the
    /// clock the later slices read.
    /// </summary>
    public class RebellionDB : BaseDataBlob
    {
        /// <summary>Days the reaction window lasts once a province rebels — the time to respond before it resolves.</summary>
        public const double ReactionWindowDays = 180.0;

        /// <summary>Legitimacy must climb back to at least this to QUELL an active rebellion. Set ABOVE the collapse
        /// threshold (hysteresis) so a province hovering on the line doesn't flicker in and out of rebellion.</summary>
        public const double RecoveryThreshold = 35.0;

        /// <summary>True while the province is actively in rebellion (hostile-but-not-yet-gone).</summary>
        [JsonProperty] public bool IsRebelling { get; internal set; } = false;

        /// <summary>When the current rebellion began (game time). Meaningful only while <see cref="IsRebelling"/>.</summary>
        [JsonProperty] public DateTime StartDate { get; internal set; }

        /// <summary>When the reaction window closes — after this, the rebellion resolves (secession/defection, a later
        /// slice). Meaningful only while <see cref="IsRebelling"/>.</summary>
        [JsonProperty] public DateTime ReactionWindowEnds { get; internal set; }

        public RebellionDB() { }

        public RebellionDB(RebellionDB other)
        {
            IsRebelling = other.IsRebelling;
            StartDate = other.StartDate;
            ReactionWindowEnds = other.ReactionWindowEnds;
        }

        public override object Clone() => new RebellionDB(this);

        /// <summary>True once an active rebellion's reaction window has lapsed at <paramref name="now"/> — the cue
        /// the (later) resolution slice reads to secede/defect the province. No-op while not rebelling.</summary>
        public bool WindowExpired(DateTime now) => IsRebelling && now >= ReactionWindowEnds;
    }
}
