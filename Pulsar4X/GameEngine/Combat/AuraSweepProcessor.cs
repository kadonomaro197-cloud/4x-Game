using System;
using System.Collections.Generic;
using Pulsar4X.Engine;
using Pulsar4X.Interfaces;
using Pulsar4X.Movement;
using Pulsar4X.Orbital;
using Pulsar4X.Ships;

namespace Pulsar4X.Combat
{
    /// <summary>
    /// THE AURA SWEEP (E14 auras, slice 2) — the per-tick pass that makes a projector's field actually reach the
    /// units around it. A hotloop (5 s, the <see cref="Hazards.SpaceHazardProcessor"/> cadence) keyed to
    /// <see cref="AuraProjectorDB"/>: for every ship in the system it takes the STRONGEST in-range field of each kind
    /// (never the sum — <see cref="AuraTools.BestOf"/>), IFF-filtered by the field's <see cref="AuraTarget"/> and
    /// distance-tapered by <see cref="AuraTools.MagnitudeAt"/>, and records the result on that ship's
    /// <see cref="AuraBuffDB"/>. It re-derives from scratch each pass and removes the record from a ship that has
    /// left every field, so the buff never accumulates or goes stale (the <c>ColonyMoraleDB</c> discipline).
    ///
    /// <para><b>Byte-identical.</b> Nothing reads <see cref="AuraBuffDB"/> yet (the combat-read is the next slice), no
    /// base-mod component mounts an <see cref="AuraAtb"/> (so no <see cref="AuraProjectorDB"/> is ever created and the
    /// sweep sleeps on every system — gotcha L5), AND the whole sweep is gated behind <see cref="EnableAuraSweep"/>
    /// (default OFF). Grave rung: a destroyed/uninstalled projector drops its <see cref="AuraProjectorDB"/> entry, so it
    /// stops contributing on the very next sweep.</para>
    ///
    /// <para><b>v1 target = space ships only.</b> The distance test needs a <c>PositionDB</c>; ground units are data
    /// objects inside a <c>GroundForcesDB</c> with no position, so a ground aura is a separate hex-distance sweep
    /// (deferred — the same space/ground split the combat resolver already lives with). Rally/Dread need a
    /// unit-morale field that does not exist yet, and Jamming needs a detection channel — so slice 2 wires only
    /// Command→firepower and Ward→toughness (the two effects with a live combat sink).</para>
    /// </summary>
    public class AuraSweepProcessor : IHotloopProcessor
    {
        /// <summary>Master gate — OFF leaves the sweep a no-op so the whole feature is byte-identical until the
        /// combat-read slice + a base-mod projector template land and the developer flips it on.</summary>
        public static bool EnableAuraSweep = false;

        public void Init(Game game) { }

        public TimeSpan RunFrequency => TimeSpan.FromSeconds(5);
        public TimeSpan FirstRunOffset => TimeSpan.FromSeconds(0);
        public Type GetParameterType => typeof(AuraProjectorDB);

        public void ProcessEntity(Entity entity, int deltaSeconds)
        {
            if (entity?.Manager != null)
                SweepManager(entity.Manager);
        }

        public int ProcessManager(EntityManager manager, int deltaSeconds) => SweepManager(manager);

        /// <summary>Runs the whole sweep once over one manager. Returns the projector-marker count so the scheduler
        /// keeps the processor awake while any projector exists and lets it sleep when none do (gotcha L5).</summary>
        private static int SweepManager(EntityManager manager)
        {
            var projectorDbs = new List<AuraProjectorDB>(manager.GetAllDataBlobsOfType<AuraProjectorDB>());
            if (!EnableAuraSweep || projectorDbs.Count == 0)
                return projectorDbs.Count;

            // Snapshot each live projector: its centre (world position) + owning faction + every field it carries.
            var projectors = new List<(Vector3 centre, int factionId, AuraProjectorField field)>();
            foreach (var pdb in projectorDbs)
            {
                var owner = pdb.OwningEntity;
                if (owner == null || !owner.IsValid)
                    continue;
                if (!owner.TryGetDataBlob<PositionDB>(out var ppos))
                    continue;
                Vector3 centre = ppos.AbsolutePosition;
                foreach (var f in pdb.Projectors)
                    projectors.Add((centre, owner.FactionOwnerID, f));
            }
            if (projectors.Count == 0)
                return projectorDbs.Count;

            foreach (var shipInfo in manager.GetAllDataBlobsOfType<ShipInfoDB>())
            {
                var ship = shipInfo.OwningEntity;
                if (ship == null || !ship.IsValid)
                    continue;
                if (!ship.TryGetDataBlob<PositionDB>(out var spos))
                    continue;
                Vector3 target = spos.AbsolutePosition;

                double bestFire = 0.0;
                double bestTough = 0.0;
                foreach (var (centre, projFaction, f) in projectors)
                {
                    if (!Affects(f.Target, projFaction, ship.FactionOwnerID))
                        continue;
                    if (!AuraTools.InRange(centre, target, f.Radius_m))
                        continue;
                    double dist = (target - centre).Length();
                    double mag = AuraTools.MagnitudeAt(f.Magnitude, dist, f.Radius_m);
                    if (mag <= 0)
                        continue;

                    // Take the strongest single field of each kind — overlapping auras do NOT stack.
                    switch (f.Effect)
                    {
                        case AuraEffect.Command: if (mag > bestFire) bestFire = mag; break;
                        case AuraEffect.Ward: if (mag > bestTough) bestTough = mag; break;
                        // Rally/Dread (unit-morale field pending) and Jamming (detection channel) carry no sink yet.
                    }
                }

                if (bestFire > 0.0 || bestTough > 0.0)
                {
                    if (!ship.TryGetDataBlob<AuraBuffDB>(out var buff))
                    {
                        buff = new AuraBuffDB();
                        ship.SetDataBlob(buff);
                    }
                    buff.Firepower = bestFire;
                    buff.Toughness = bestTough;
                }
                else if (ship.TryGetDataBlob<AuraBuffDB>(out _))
                {
                    // Left every field this pass → drop the stale record (grave rung for the buff).
                    ship.RemoveDataBlob<AuraBuffDB>();
                }
            }

            return projectorDbs.Count;
        }

        /// <summary>IFF gate — does a field aimed at <paramref name="target"/> land on a ship of
        /// <paramref name="shipFaction"/> given the projector's <paramref name="projectorFaction"/>?</summary>
        internal static bool Affects(AuraTarget target, int projectorFaction, int shipFaction)
        {
            switch (target)
            {
                case AuraTarget.Friends: return shipFaction == projectorFaction;
                case AuraTarget.Foes: return shipFaction != projectorFaction;
                case AuraTarget.Everyone: return true;
                default: return false;
            }
        }
    }
}
