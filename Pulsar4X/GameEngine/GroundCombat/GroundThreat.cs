using System.Collections.Generic;
using Pulsar4X.Engine;
using Pulsar4X.Galaxy;   // PlanetRegionsDB, Region

namespace Pulsar4X.GroundCombat
{
    /// <summary>
    /// THE FOG-HONEST ENEMY-STRENGTH READ (Operation Earthfall G2.2a) — the ground echo of the space AI's
    /// <c>Factions.ThreatAssessment.DetectedStrengthOf</c>: a battalion weighs its own firepower against the enemy
    /// firepower it can actually SEE, never against omniscient truth. This is the "fog slice 5" the surface-fog design
    /// (<c>docs/ground/SURFACE-FOG-AND-RECON-DESIGN.md</c>) promoted from a someday-item to a REQUIREMENT — the load-
    /// bearing new read the ground tactical brain (<see cref="GroundTactics"/>) reads for its odds, and the read PW's
    /// landing score consumes for "which region is easiest to land in." One read, two consumers, built once here.
    ///
    /// The rule (matching <c>CombatRisk</c>'s undetected-clears rule in space): <b>an UNDETECTED enemy counts ZERO.</b>
    /// A region is "detected" by a viewer faction when — and only when — the viewer is IN CONTACT with it (a unit of
    /// theirs stands there), OWNS it (it holds the region → sees what's on its own ground), or has SCOUTED it via the
    /// per-faction ground fog (<see cref="PlanetRegionsDB.IsRegionRevealedFor"/> — a radar sweep or a space survey to
    /// THAT faction). Enemy garrisons in un-scouted enemy territory are invisible → strength 0 → the recon→decision loop
    /// closes cradle-to-grave (a ground scout genuinely raises the AI's confidence). Deliberately does NOT read the
    /// world-level <see cref="Region.Surveyed"/> flag — that's the DEFENDER's knowledge and would leak the whole world
    /// to an attacker who never scouted it.
    ///
    /// Pure / deterministic / defensive (never throws — it runs inside the ground hotloop's brain step, L4). Firepower
    /// is summed as Σ living-unit <see cref="GroundUnit.Attack"/> — the SAME unit as
    /// <c>GroundFormationTools.FormationStrength</c>, so own-vs-enemy is an apples-to-apples ratio (unlike the space AI's
    /// combat-value-vs-signal-kW mismatch).
    /// </summary>
    public static class GroundThreat
    {
        /// <summary>Σ living <see cref="GroundUnit.Attack"/> of units NOT owned by <paramref name="viewerFactionId"/>
        /// (and not the neutral faction) standing in region <paramref name="regionIndex"/>. The raw per-region enemy
        /// firepower — NO fog gate here (the caller applies detection); pure and null-safe.</summary>
        public static double EnemyStrengthInRegion(GroundForcesDB forces, int regionIndex, int viewerFactionId)
        {
            if (forces?.Units == null) return 0.0;
            double sum = 0.0;
            foreach (var u in forces.Units)
            {
                if (u == null || u.Health <= 0) continue;
                if (u.RegionIndex != regionIndex) continue;
                if (u.FactionOwnerID == viewerFactionId) continue;         // mine, not an enemy
                if (u.FactionOwnerID == Game.NeutralFactionId) continue;   // neutral ground presence isn't a threat
                sum += u.Attack;
            }
            return sum;
        }

        /// <summary>Is region <paramref name="regionIndex"/> DETECTED by <paramref name="viewerFactionId"/>? True when
        /// the viewer is standing there (<paramref name="viewerRegion"/> — contact; pass -1 for "no contact region"),
        /// OWNS it, or has SCOUTED it (per-faction reveal). This is the ONE place the fog rule is defined; both the
        /// brain and the landing score read through it so they can't drift. Bounds-safe.</summary>
        public static bool IsRegionDetected(PlanetRegionsDB regionsDB, int viewerFactionId, int regionIndex, int viewerRegion)
        {
            if (regionsDB == null || regionIndex < 0 || regionIndex >= regionsDB.Regions.Count) return false;
            if (regionIndex == viewerRegion) return true;                                   // I'm standing there — contact
            if (regionsDB.Regions[regionIndex].OwnerFactionID == viewerFactionId) return true; // I hold it — I see my own ground
            return regionsDB.IsRegionRevealedFor(viewerFactionId, regionIndex);             // scouted / surveyed to me
        }

        /// <summary>THE BRAIN READ: the fog-limited enemy firepower a battalion faces at <paramref name="regionIndex"/> —
        /// its OWN region (always counted; you're in contact) PLUS every ADJACENT region the viewer has DETECTED. An
        /// undetected neighbour contributes ZERO (the fog rule). Reads the body's <see cref="GroundForcesDB"/> +
        /// <see cref="PlanetRegionsDB"/>. Defensive: a body with no roster / no region layer reads 0.</summary>
        public static double DetectedEnemyStrength(Entity body, int viewerFactionId, int regionIndex)
        {
            if (body == null
                || !body.TryGetDataBlob<GroundForcesDB>(out var forces)
                || !body.TryGetDataBlob<PlanetRegionsDB>(out var regionsDB)
                || regionIndex < 0 || regionIndex >= regionsDB.Regions.Count)
                return 0.0;
            return DetectedEnemyStrength(forces, regionsDB, viewerFactionId, regionIndex);
        }

        /// <summary>The pure core of <see cref="DetectedEnemyStrength(Entity,int,int)"/> (forces + regions supplied) —
        /// so the fog math is unit-testable without a full body/manager.</summary>
        public static double DetectedEnemyStrength(GroundForcesDB forces, PlanetRegionsDB regionsDB, int viewerFactionId, int regionIndex)
        {
            if (forces == null || regionsDB == null || regionIndex < 0 || regionIndex >= regionsDB.Regions.Count) return 0.0;

            double sum = EnemyStrengthInRegion(forces, regionIndex, viewerFactionId);   // own region — always in contact
            var neighbors = regionsDB.Regions[regionIndex].Neighbors;
            if (neighbors != null)
                foreach (var n in neighbors)
                    if (IsRegionDetected(regionsDB, viewerFactionId, n, regionIndex))
                        sum += EnemyStrengthInRegion(forces, n, viewerFactionId);
            return sum;
        }

        /// <summary>The enemy firepower in the viewer's OWN region ONLY (region <paramref name="regionIndex"/>) — the
        /// "am I in contact right here?" read the brain uses so an attacker CLEARS a region before advancing out of it
        /// (you never march past a live enemy that's shooting you). Own-region enemies are always detected (contact).</summary>
        public static double EnemyStrengthHere(Entity body, int viewerFactionId, int regionIndex)
        {
            if (body == null || !body.TryGetDataBlob<GroundForcesDB>(out var forces)) return 0.0;
            return EnemyStrengthInRegion(forces, regionIndex, viewerFactionId);
        }

        /// <summary>Is the viewer BLIND around <paramref name="regionIndex"/>? True when at least one ADJACENT region is
        /// NOT detected — an un-scouted neighbour could hide a massing enemy, so the brain treats unknown as risk (the
        /// design's "blind → bias cautious"). A viewer that has scouted (or owns) all its neighbours is not blind.
        /// Defensive: no region layer → not blind (nothing to be blind about).</summary>
        public static bool IsBlind(Entity body, int viewerFactionId, int regionIndex)
        {
            if (body == null || !body.TryGetDataBlob<PlanetRegionsDB>(out var regionsDB)
                || regionIndex < 0 || regionIndex >= regionsDB.Regions.Count) return false;
            var neighbors = regionsDB.Regions[regionIndex].Neighbors;
            if (neighbors == null) return false;
            foreach (var n in neighbors)
                if (!IsRegionDetected(regionsDB, viewerFactionId, n, regionIndex))
                    return true;
            return false;
        }

        /// <summary>THE LANDING-SCORE READ (PW consumer): the total DEFENDER firepower an attacker
        /// (<paramref name="viewerFactionId"/>) can SEE across the whole body — Σ enemy strength over every region the
        /// attacker has DETECTED (owned/scouted; no contact region, so pass -1). A world the attacker has surveyed reads
        /// its real defence; an un-scouted one reads low (fog) — so the "easiest landing" choice is fog-honest, never
        /// omniscient. One read, shared with the brain above. Defensive.</summary>
        public static double DetectedDefenderStrength(Entity body, int viewerFactionId)
        {
            if (body == null
                || !body.TryGetDataBlob<GroundForcesDB>(out var forces)
                || !body.TryGetDataBlob<PlanetRegionsDB>(out var regionsDB))
                return 0.0;
            double sum = 0.0;
            for (int i = 0; i < regionsDB.Regions.Count; i++)
                if (IsRegionDetected(regionsDB, viewerFactionId, i, -1))
                    sum += EnemyStrengthInRegion(forces, i, viewerFactionId);
            return sum;
        }

        /// <summary>THE CLIENT FOG READ (C2 consumer): every LIVING enemy <see cref="GroundUnit"/> on the body that
        /// <paramref name="viewerFactionId"/> can actually SEE — i.e. standing in a region the viewer has DETECTED
        /// (owned / scouted / surveyed-to-them; pass no contact region → -1), by the SAME
        /// <see cref="IsRegionDetected"/> rule <see cref="DetectedDefenderStrength"/> uses. An enemy in un-scouted
        /// territory is NOT returned (fog-honest — the client rings/tokens only the SEEN, matching the space contact
        /// blips). Neutral-owned units are excluded (not enemies). Returns an empty list on a body with no roster / no
        /// region layer. Pure/defensive; never throws.</summary>
        public static List<GroundUnit> DetectedEnemyUnits(Entity body, int viewerFactionId)
        {
            var result = new List<GroundUnit>();
            if (body == null
                || !body.TryGetDataBlob<GroundForcesDB>(out var forces)
                || forces.Units == null
                || !body.TryGetDataBlob<PlanetRegionsDB>(out var regionsDB))
                return result;
            // CONTACT regions — where the viewer has a living unit standing (you SEE the enemies you're fighting), so a
            // contested neutral region you're in isn't fogged even before you own/scout it. This is the client's "boots
            // on the ground = eyes on the ground," on top of the owned/scouted rule (the space contact-blip equivalent).
            var contactRegions = new HashSet<int>();
            foreach (var u in forces.Units)
                if (u != null && u.Health > 0 && u.FactionOwnerID == viewerFactionId && u.RegionIndex >= 0)
                    contactRegions.Add(u.RegionIndex);
            foreach (var u in forces.Units)
            {
                if (u == null || u.Health <= 0) continue;
                if (u.FactionOwnerID == viewerFactionId) continue;             // mine
                if (u.FactionOwnerID == Game.NeutralFactionId) continue;       // neutral isn't an enemy
                if (u.RegionIndex < 0 || u.RegionIndex >= regionsDB.Regions.Count) continue;
                bool detected = contactRegions.Contains(u.RegionIndex)                        // I'm standing there — contact
                             || IsRegionDetected(regionsDB, viewerFactionId, u.RegionIndex, -1); // owned / scouted / surveyed to me
                if (!detected) continue;   // fog: an un-scouted, un-contacted enemy is invisible
                result.Add(u);
            }
            return result;
        }
    }
}
