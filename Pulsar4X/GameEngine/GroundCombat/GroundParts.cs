using System.Collections.Generic;
using Pulsar4X.Engine;
using Pulsar4X.Components;
using Pulsar4X.Factions;
using Pulsar4X.Construction;

namespace Pulsar4X.GroundCombat
{
    /// <summary>
    /// SURFACE PARTS HAULAGE — landing crated component parts onto a planet's surface so a combat engineer (a chassis
    /// carrying a <see cref="GroundConstructorAtb"/>) can later assemble a footprint building on site, with NO colony
    /// present (the beachhead enabler the A5 ledger flagged: "surface component haulage — <see cref="GroundTransportDB"/>
    /// carries only GroundUnits today"). The parts live in a per-region pool on the body's <see cref="GroundForcesDB"/>
    /// (<see cref="GroundForcesDB.SurfaceParts"/>) — the parallel to units standing on the roster: a body holds units AND
    /// the crated parts landed beside them. A crate is a save-safe <see cref="SurfacePart"/> <c>(designId, count)</c>
    /// record, exactly how a cargo hold stores a component (an <c>ICargoable</c> keyed by its design), so nothing heavy
    /// rides the save.
    ///
    /// Why the surface (the body) and not a ship manifest: the surface is where parts must END UP for the engineer that
    /// stands in the region to build from them; a ship's cargo hold ALREADY carries components (a component built with no
    /// install target drops into cargo — <c>ComponentDesign.OnConstructionComplete</c>), so the CARRY half needs no new
    /// container. The missing piece is the destination + the land step — which is all this adds, keeping the change one
    /// save-safe list on the existing per-body blob (the least-invasive option per the two candidates).
    ///
    /// Two entry points: <see cref="AddParts"/> is the primitive (a crate lands on the surface); <see cref="LandPartsFromShip"/>
    /// is the full haul — draw the crated parts from a ship's (and its fleet-mates') cargo holds and set them on the
    /// surface, gated on the ship being AT the body and holding the orbit (the same gate that lets troops land). All
    /// defensive: they return 0 / false rather than throw (L4). The on-site build that CONSUMES a surface pool into a
    /// building is a LATER G1 slice; this slice lands the parts and makes them readable.
    /// </summary>
    public static class GroundParts
    {
        /// <summary>Land <paramref name="count"/> crated units of component <paramref name="designId"/> onto
        /// <paramref name="body"/>'s region <paramref name="regionIndex"/> surface pool (merging into an existing crate of
        /// the same design + region). Creates the ground-forces roster on demand (like <see cref="GroundForces.RaiseUnit"/>).
        /// Returns the crate's new total for that design in that region, or 0 if nothing was added (bad args). Never throws.</summary>
        public static int AddParts(Entity body, int regionIndex, string designId, int count)
        {
            if (body == null || string.IsNullOrEmpty(designId) || count <= 0 || regionIndex < 0) return 0;
            if (!body.TryGetDataBlob<GroundForcesDB>(out var forces))
            {
                forces = new GroundForcesDB();
                body.SetDataBlob(forces);
            }
            foreach (var p in forces.SurfaceParts)
                if (p.RegionIndex == regionIndex && p.DesignId == designId)
                { p.Count += count; return p.Count; }
            forces.SurfaceParts.Add(new SurfacePart(regionIndex, designId, count));
            return count;
        }

        /// <summary>Units of a specific crated <paramref name="designId"/> on region <paramref name="regionIndex"/>'s
        /// surface (0 if none / no roster). A read accessor so the client and the on-site-build order needn't touch the
        /// blob's list.</summary>
        public static int PartCount(Entity body, int regionIndex, string designId)
        {
            if (body == null || string.IsNullOrEmpty(designId) || !body.TryGetDataBlob<GroundForcesDB>(out var forces)) return 0;
            foreach (var p in forces.SurfaceParts)
                if (p.RegionIndex == regionIndex && p.DesignId == designId) return p.Count;
            return 0;
        }

        /// <summary>All crated parts on region <paramref name="regionIndex"/>'s surface as designId → count. Never null;
        /// empty if none landed (a region holds at most one crate per design — <see cref="AddParts"/> merges).</summary>
        public static Dictionary<string, int> PartsInRegion(Entity body, int regionIndex)
        {
            var map = new Dictionary<string, int>();
            if (body == null || !body.TryGetDataBlob<GroundForcesDB>(out var forces)) return map;
            foreach (var p in forces.SurfaceParts)
                if (p.RegionIndex == regionIndex && p.Count > 0)
                    map[p.DesignId] = p.Count;
            return map;
        }

        /// <summary>CONSUME <paramref name="count"/> crated units of <paramref name="designId"/> from region
        /// <paramref name="regionIndex"/>'s surface pool — the on-site-build draw (the combat engineer erects a footprint
        /// building out of one landed crate). CHECK-THEN-CONSUME: a short crate takes NOTHING (returns false), so a build
        /// can't half-drain a pool; an emptied crate is dropped from the list. Returns true iff the parts were consumed.
        /// Never throws. The counterpart of <see cref="AddParts"/>; used by <see cref="GroundBeachhead"/> on completion.</summary>
        public static bool ConsumePart(Entity body, int regionIndex, string designId, int count)
        {
            if (body == null || string.IsNullOrEmpty(designId) || count <= 0 || regionIndex < 0) return false;
            if (!body.TryGetDataBlob<GroundForcesDB>(out var forces) || forces.SurfaceParts == null) return false;
            for (int i = 0; i < forces.SurfaceParts.Count; i++)
            {
                var p = forces.SurfaceParts[i];
                if (p.RegionIndex != regionIndex || p.DesignId != designId) continue;
                if (p.Count < count) return false;              // short crate → consume nothing (check-then-consume)
                p.Count -= count;
                if (p.Count <= 0) forces.SurfaceParts.RemoveAt(i);
                return true;
            }
            return false;
        }

        /// <summary>The full HAUL: draw <paramref name="count"/> crated units of <paramref name="designId"/> from
        /// <paramref name="ship"/>'s pooled cargo holds (its own + its fleet-mates', via <see cref="ConstructionCargo"/>)
        /// and land them onto <paramref name="targetBody"/>'s region <paramref name="regionIndex"/>. Gated exactly like
        /// landing troops (<see cref="GroundTransport.TryLandUnit"/>): the ship must be AT the body and hold the orbit.
        /// CHECK-THEN-CONSUME so a short pool lands nothing (RemoveCargoByUnit clamps silently — a spend-then-discover
        /// path would half-drain a hold). Returns true iff the parts were drawn and landed. Never throws.</summary>
        public static bool LandPartsFromShip(Entity ship, Entity targetBody, int regionIndex, string designId, int count)
        {
            if (ship == null || targetBody == null || string.IsNullOrEmpty(designId) || count <= 0 || regionIndex < 0) return false;
            if (!GroundTransport.ShipIsAtBody(ship, targetBody)) return false;
            if (!GroundTransport.HasOrbitalControl(ship, targetBody)) return false;

            // resolve the crated part's design (an ICargoable) off the ship's faction, the way GroundBuild resolves a design
            var game = ship.Manager?.Game;
            if (game == null || !game.Factions.TryGetValue(ship.FactionOwnerID, out var factionEntity)) return false;
            if (!factionEntity.TryGetDataBlob<FactionInfoDB>(out var factionInfo)) return false;
            if (!factionInfo.IndustryDesigns.TryGetValue(designId, out var design) || design is not ComponentDesign) return false;
            var comp = (ComponentDesign)design;   // an ICargoable — the way a cargo hold keys a stored component

            var holds = ConstructionCargo.GatherPooledHolds(ship);
            if (!ConstructionCargo.TryConsumePooled(holds, comp, count)) return false;   // short pool → nothing consumed

            AddParts(targetBody, regionIndex, designId, count);
            return true;
        }
    }
}
