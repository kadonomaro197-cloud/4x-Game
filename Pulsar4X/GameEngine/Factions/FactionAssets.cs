using System.Collections.Generic;
using Pulsar4X.Engine;

namespace Pulsar4X.Factions
{
    /// <summary>
    /// Live-owner-verified enumeration of a faction's off-world HOLDINGS — its colonies and its stations
    /// (FORCES-WINDOW-DESIGN §S9). This is the "cross-check" the Force-Management roster needs before it lists a
    /// faction's colonies/stations as rows.
    ///
    /// <para><b>WHY THIS EXISTS (the capture-stale trap).</b> <see cref="FactionInfoDB.Colonies"/> and
    /// <see cref="FactionInfoDB.Stations"/> are <i>registries</i> — written once when the asset is created
    /// (<c>ColonyFactory</c> / <c>StationFactory</c>) and <b>not maintained on capture</b>. When a planet is taken by
    /// ground invasion, <c>GroundForcesProcessor</c> flips the colony entity's live <see cref="Entity.FactionOwnerID"/>
    /// (<c>GroundForcesProcessor.cs</c>, marked "v1: ownership flip; deeper transfer later") but leaves BOTH registries
    /// untouched. So a faction's <c>Colonies</c> list can still name a colony it has <b>lost</b>. The honest read is:
    /// the registry is the candidate set, the live <see cref="Entity.FactionOwnerID"/> is the truth, and only an asset
    /// the faction STILL owns is returned.</para>
    ///
    /// <para><b>KNOWN LIMIT — the GAIN direction is not covered here.</b> Because capture also never ADDS the taken
    /// colony to the captor's registry, a colony a faction has <i>captured</i> won't appear in its own
    /// <c>Colonies</c> list, so this filter can't surface it either. Making a captured world show up for its new owner
    /// is a <b>capture-side</b> fix (register the asset with the new owner where the ownership flips), tracked as a
    /// follow-up; it is deliberately out of scope for this read-only cross-check.</para>
    ///
    /// Pure / read-only / defensive — safe to call every frame from the UI, never throws.
    /// </summary>
    public static class FactionAssets
    {
        /// <summary>
        /// The colonies a faction STILL owns: its <see cref="FactionInfoDB.Colonies"/> registry filtered to the
        /// entries whose live <see cref="Entity.FactionOwnerID"/> still equals this faction's id. A colony lost to
        /// capture (its owner flipped, but still lingering in the registry) is dropped. Returns an empty list for a
        /// null / unmanaged / faction-info-less entity — never throws.
        /// </summary>
        public static List<Entity> OwnedColonies(Entity faction)
        {
            var result = new List<Entity>();
            // A Manager guard is what keeps "never throws" true: Entity.TryGetDataBlob delegates to Manager, so a
            // Manager-less entity (e.g. a bare Entity.Create()) would NRE without it.
            if (faction == null || faction.Manager == null
                || !faction.TryGetDataBlob<FactionInfoDB>(out var info) || info.Colonies == null)
                return result;

            foreach (var colony in info.Colonies)
                if (colony != null && colony.FactionOwnerID == faction.Id)
                    result.Add(colony);
            return result;
        }

        /// <summary>
        /// The stations a faction STILL owns — the <see cref="FactionInfoDB.Stations"/> sibling of
        /// <see cref="OwnedColonies"/>, same live-owner filter. Returns an empty list for a null / unmanaged /
        /// faction-info-less entity — never throws.
        /// </summary>
        public static List<Entity> OwnedStations(Entity faction)
        {
            var result = new List<Entity>();
            if (faction == null || faction.Manager == null
                || !faction.TryGetDataBlob<FactionInfoDB>(out var info) || info.Stations == null)
                return result;

            foreach (var station in info.Stations)
                if (station != null && station.FactionOwnerID == faction.Id)
                    result.Add(station);
            return result;
        }
    }
}
