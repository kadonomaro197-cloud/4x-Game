using System;
using Newtonsoft.Json;
using Pulsar4X.Datablobs;

namespace Pulsar4X.Combat
{
    /// <summary>A fleet's weapons-release posture — the FIRST ROE knob (Phase 3, docs/AUTO-RESOLVER-GROUND-TRUTH-2026-07-29.md §14.4).
    /// A battle only erupts if someone will release a shot; two non-WeaponsFree fleets in range sit in a tense standoff.</summary>
    public enum EngagementPosture
    {
        /// <summary>Fire when an enemy is detected and in range — the default; a weapons-free fleet STARTS battles.</summary>
        WeaponsFree,
        /// <summary>Hold fire — never shoot first. Two weapons-hold fleets in range sit tense, no battle forms.</summary>
        WeaponsHold,
        /// <summary>Fire only if fired upon — won't START a battle, but defends once one is underway.</summary>
        ReturnFire,
    }

    /// <summary>
    /// A fleet's ACTIVE combat posture — the doctrine the player (or NPC) has chosen for it. Set by copying a
    /// <c>CombatDoctrineBlueprint</c> from the moddable catalog via <see cref="FleetDoctrine.TrySetDoctrine"/>.
    /// The auto-resolver reads the multipliers as a read-time modifier on this fleet's strength/toughness (the
    /// BonusesDB pattern) — nothing is baked into ship stats, so switching is reversible.
    ///
    /// NOT the same as <c>FactionInfoDB.Doctrine</c> (the strategic AI vector) — same word, different thing.
    /// </summary>
    public class FleetDoctrineDB : BaseDataBlob
    {
        [JsonProperty] public string DoctrineId { get; internal set; } = "";
        [JsonProperty] public string Family { get; internal set; } = "";
        [JsonProperty] public double FirepowerMult { get; internal set; } = 1.0;
        [JsonProperty] public double ToughnessMult { get; internal set; } = 1.0;
        [JsonProperty] public double SpeedMult { get; internal set; } = 1.0;
        [JsonProperty] public bool IsRetreat { get; internal set; } = false;

        /// <summary>The fleet's weapons-release posture (Phase 3, the first ROE knob). Default WeaponsFree = fights on
        /// contact (the pre-P3 behaviour). The full Rules-of-Engagement set (closing intent, target priority) grows
        /// here in Phase 5.</summary>
        [JsonProperty] public EngagementPosture Posture { get; internal set; } = EngagementPosture.WeaponsFree;

        /// <summary>WHO this fleet shoots first — the target-selection half of its doctrine (Phase 5 ROE, wired
        /// 2026-08-04). Default <see cref="TargetPriority.Balanced"/> = the legacy spread-fire-by-health behaviour, so
        /// a fleet with no authored priority fights EXACTLY as before. Copied from the catalog by
        /// <see cref="FleetDoctrine.TrySetDoctrine"/> and read by the resolver's casualty step
        /// (<c>CombatEngagement.ApplyCasualties</c>). In the current whole-or-dead aggregate model only
        /// <see cref="TargetPriority.Heaviest"/> (sort by toughness) and <see cref="TargetPriority.BiggestThreat"/>
        /// (sort by firepower) change behaviour; <see cref="TargetPriority.FinishWounded"/> (needs per-ship health,
        /// i.e. the parked degrade-on-damage model) and <see cref="TargetPriority.Closest"/> /
        /// <see cref="TargetPriority.Backfield"/> (need per-target position) fall back to Balanced until those models
        /// land — flagged, not faked.</summary>
        [JsonProperty] public TargetPriority Targeting { get; internal set; } = TargetPriority.Balanced;

        /// <summary>Game time at/after which this fleet may switch posture again (the switch cooldown clock).</summary>
        [JsonProperty] public DateTime SwitchableAfter { get; internal set; } = DateTime.MinValue;

        public FleetDoctrineDB() { }

        public FleetDoctrineDB(FleetDoctrineDB db)
        {
            DoctrineId = db.DoctrineId;
            Family = db.Family;
            FirepowerMult = db.FirepowerMult;
            ToughnessMult = db.ToughnessMult;
            SpeedMult = db.SpeedMult;
            IsRetreat = db.IsRetreat;
            Posture = db.Posture;
            Targeting = db.Targeting;
            SwitchableAfter = db.SwitchableAfter;
        }

        public override object Clone() => new FleetDoctrineDB(this);
    }
}
