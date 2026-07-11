using System;
using System.Collections.Generic;
using System.Linq;
using Pulsar4X.Colonies;
using Pulsar4X.Combat;
using Pulsar4X.Engine;

namespace Pulsar4X.Factions
{
    /// <summary>
    /// F-A3 (docs/AI-BRAIN-BUILD-TRACKER.md, Movement I — Foundations): faction-tier roll-up GAUGES.
    ///
    /// Pure, read-only helpers that sum the per-colony gauges (population / morale / legitimacy) and the ledger
    /// up to the whole-faction tier the NPC needs-ladder reads (the "Survive / Stabilize" instruments in
    /// docs/AI-OBJECTIVE-ENGINE-DESIGN.md). The engine already recomputes morale and legitimacy per colony every
    /// month; nothing aggregated them to the empire tier — this is that missing reader, and nothing else.
    ///
    /// It writes NOTHING and touches no existing code path, so it is byte-identical to the sim as it stands: a new
    /// gauge you can read, not a new behaviour. Defensive throughout (a missing blob is skipped, a pool-less/
    /// colony-less faction reads a sane default), so it never throws when read from a hotloop later.
    ///
    /// Deliberately NOT here yet: the MILITARY-strength roll-up. Summing a faction's own ship combat values needs
    /// cross-system entity enumeration and pairs naturally with the fog-limited ENEMY-strength estimate — both land
    /// together in the "eyes" foundation (F-B1, ThreatEstimate). Kept out so this slice stays small and certain.
    /// </summary>
    public static class FactionRollup
    {
        /// <summary>The faction's cash balance — the economy-solvency gauge. 0 for an entity with no faction blob.</summary>
        public static decimal Balance(Entity faction)
            => FactionInfo(faction)?.Money?.GetCurrentFunds() ?? 0m;

        /// <summary>Total living population across all the faction's colonies (every species summed). 0 if none.</summary>
        public static long TotalPopulation(Entity faction)
        {
            var info = FactionInfo(faction);
            if (info == null) return 0;

            long total = 0;
            foreach (var colony in info.Colonies)
                total += ColonyPopulation(colony);
            return total;
        }

        /// <summary>How many colonies the faction holds.</summary>
        public static int ColonyCount(Entity faction) => FactionInfo(faction)?.Colonies.Count ?? 0;

        /// <summary>
        /// Population-weighted mean colony MORALE — a big unhappy world outweighs a tiny content one, which is what
        /// "how content is the empire" actually means. Colonies with no morale blob are skipped; a faction with no
        /// morale-bearing colony reads the Neutral midpoint (50), because "no data" is not "in crisis".
        /// </summary>
        public static double MeanMorale(Entity faction)
            => WeightedColonyMean(faction, ColonyMoraleDB.Neutral,
                colony => colony.TryGetDataBlob<ColonyMoraleDB>(out var m) ? m.Morale : (double?)null);

        /// <summary>Population-weighted mean colony LEGITIMACY (same rule as <see cref="MeanMorale"/>).</summary>
        public static double MeanLegitimacy(Entity faction)
            => WeightedColonyMean(faction, LegitimacyDB.Neutral,
                colony => colony.TryGetDataBlob<LegitimacyDB>(out var l) ? l.Legitimacy : (double?)null);

        /// <summary>
        /// F-B1a (docs/AI-BRAIN-BUILD-TRACKER.md, the "eyes" foundation): the faction's OWN total military strength —
        /// the sum, over every ship it owns in every system, of that ship's combat value (<see cref="ShipCombatValueDB"/>
        /// Firepower + Toughness, the two numbers the auto-resolver already rates a ship on, kept on their intentionally
        /// shared joule-scale). This is the own-strength half of the Risk/threat read; the fog-limited ENEMY estimate is
        /// F-B1b. Deferred out of F-A3 because — unlike the colony gauges — it needs to walk every system's entities, so
        /// it takes the game off the faction entity's manager. Read-only → byte-identical. 0 for a fleetless faction.
        /// </summary>
        public static double MilitaryStrength(Entity faction)
        {
            var info = FactionInfo(faction);
            var game = faction?.Manager?.Game;
            if (info == null || game == null)
                return 0;

            int factionId = faction.Id;
            double total = 0;
            foreach (var system in game.Systems)
            {
                foreach (var ship in system.GetAllEntitiesWithDataBlob<ShipCombatValueDB>())
                {
                    if (ship.FactionOwnerID == factionId && ship.TryGetDataBlob<ShipCombatValueDB>(out var cv))
                        total += cv.Firepower + cv.Toughness;
                }
            }
            return total;
        }

        // --- helpers ---

        private static FactionInfoDB FactionInfo(Entity faction)
            => faction != null && faction.IsValid && faction.TryGetDataBlob<FactionInfoDB>(out var info) ? info : null;

        private static long ColonyPopulation(Entity colony)
            => colony.TryGetDataBlob<ColonyInfoDB>(out var ci) && ci.Population != null
                ? ci.Population.Values.Sum()
                : 0;

        /// <summary>
        /// Population-weighted mean of a per-colony gauge. <paramref name="read"/> returns the colony's value or null
        /// (no such gauge on that colony → skip it). No gauge-bearing colony → <paramref name="neutral"/>. Colonies
        /// present but all unpopulated → the plain (unweighted) average, so an empty-but-real colony still counts.
        /// </summary>
        private static double WeightedColonyMean(Entity faction, double neutral, Func<Entity, double?> read)
        {
            var info = FactionInfo(faction);
            if (info == null) return neutral;

            double weightedSum = 0, totalWeight = 0, plainSum = 0;
            int count = 0;
            foreach (var colony in info.Colonies)
            {
                double? value = read(colony);
                if (value == null) continue;

                double pop = ColonyPopulation(colony);
                weightedSum += value.Value * pop;
                totalWeight += pop;
                plainSum += value.Value;
                count++;
            }

            if (count == 0) return neutral;                 // no gauge-bearing colony → "no data" is neutral, not crisis
            if (totalWeight <= 0) return plainSum / count;   // colonies exist but all empty → simple average
            return weightedSum / totalWeight;
        }
    }
}
