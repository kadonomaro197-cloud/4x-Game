using Newtonsoft.Json;
using Pulsar4X.Datablobs;

namespace Pulsar4X.Combat
{
    /// <summary>
    /// The aura buff currently landing on THIS ship — the sweep's OUTPUT (E14 auras, slice 2). Each field is a
    /// fractional bonus (0 = none, 0.15 = +15%), the strongest single overlapping field of that kind (the
    /// take-the-BEST-not-SUM guard-rail, <see cref="AuraTools.BestOf"/>), distance-tapered from the projector's centre.
    ///
    /// <para><b>Recomputed from scratch every sweep</b> (<see cref="AuraSweepProcessor"/> re-derives and overwrites it,
    /// and removes it entirely when a ship falls out of every field) — so it never accumulates or goes stale, the same
    /// discipline <c>ColonyMoraleDB</c> uses. This is deliberately NOT <c>People.BonusesDB</c>: that channel is a
    /// persistent accumulator read only from a fleet's FLAGSHIP commander and applied fleet-wide, which cannot express a
    /// per-ship, per-tick, distance-scaled field. <c>AuraBuffDB</c> is the per-ship record the combat-read slice will
    /// fold into each ship's firepower/toughness multiplier.</para>
    ///
    /// <para><b>Read by nothing yet → byte-identical.</b> Slice 2 builds the sweep that WRITES this; the next slice
    /// wires the combat resolver to READ it (where the per-ship-vs-flagship design fork gets settled). Populated only
    /// on ships actually inside a field, so an aura-free game carries none of these blobs.</para>
    /// </summary>
    public class AuraBuffDB : BaseDataBlob
    {
        /// <summary>Fractional firepower bonus from a friendly Command aura (0 = none). Applied as ×(1 + Firepower).</summary>
        [JsonProperty] public double Firepower { get; internal set; }

        /// <summary>Fractional damage-mitigation bonus from a friendly Ward aura (0 = none). Applied as ×(1 + Toughness).</summary>
        [JsonProperty] public double Toughness { get; internal set; }

        public AuraBuffDB() { }

        public AuraBuffDB(AuraBuffDB o)
        {
            Firepower = o.Firepower;
            Toughness = o.Toughness;
        }

        public override object Clone() => new AuraBuffDB(this);
    }
}
