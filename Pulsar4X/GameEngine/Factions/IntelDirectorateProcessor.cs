using System;
using Pulsar4X.DataStructures;
using Pulsar4X.Engine;
using Pulsar4X.Interfaces;
using Pulsar4X.People;

namespace Pulsar4X.Factions
{
    /// <summary>
    /// Espionage E2 — the RECRUITMENT arm of the spy network, the espionage twin of <see cref="Pulsar4X.People.NavalAcademyProcessor"/>.
    /// A colony's <see cref="IntelDirectorateDB"/> (built in E1) periodically recruits covert operatives
    /// (<see cref="CommanderTypes.Intelligence"/>) up to the faction's op capacity — so building more Intelligence
    /// Directorates (raising <see cref="IntelDirectorateDB.OpCapacity"/>) literally grows your stable of agents. Each
    /// recruit rolls an ExperienceCap on the academy bell curve and gets a <see cref="CommanderBonuses.RollEspionageCompetence"/>
    /// tradecraft bonus, so a recruited agent has a real skill the detection roll (E3) will read.
    ///
    /// An <see cref="IInstanceProcessor"/> (scheduled by <see cref="IntelDirectorateAtb"/> on install, then reschedules
    /// itself). It stops the moment the directorate is destroyed (no <see cref="IntelDirectorateDB"/> → early return,
    /// no reschedule) — the recruiting-stops-when-the-HQ-falls half of the grave rung. Byte-identical on the default
    /// start: no colony carries an <see cref="IntelDirectorateDB"/> until one is built, so this never fires.
    /// </summary>
    public class IntelDirectorateProcessor : IInstanceProcessor
    {
        /// <summary>Days between recruiting cycles — the directorate trains a fresh operative every ~6 months, up to
        /// capacity. Tunable balance dial (mirrors the naval academy's class cadence, faster because agents are few).</summary>
        public const int RecruitIntervalDays = 180;

        internal override void ProcessEntity(Entity entity, DateTime atDateTime)
        {
            // Directorate gone (uninstalled / destroyed) → recruiting stops, and we do NOT reschedule. The grave rung.
            if (!entity.TryGetDataBlob<IntelDirectorateDB>(out var directorate))
                return;

            var game = entity.Manager.Game;
            if (game.Factions.TryGetValue(entity.FactionOwnerID, out var faction)
                && faction.TryGetDataBlob<FactionInfoDB>(out var factionInfo))
            {
                // Recruit one operative per cycle while the faction is under its op capacity (more directorates → more agents).
                if (CountAgents(factionInfo) < directorate.OpCapacity)
                    RecruitAgent(entity, game);
            }

            // Keep topping up toward capacity on the recruiting cadence.
            var next = atDateTime + TimeSpan.FromDays(RecruitIntervalDays);
            entity.Manager.ManagerSubpulses.AddEntityInterupt(next, nameof(IntelDirectorateProcessor), entity);
        }

        /// <summary>How many <see cref="CommanderTypes.Intelligence"/> operatives the faction currently fields.</summary>
        public static int CountAgents(FactionInfoDB factionInfo)
        {
            int count = 0;
            foreach (var commander in factionInfo.Commanders)
                if (commander.TryGetDataBlob<CommanderDB>(out var cdb) && cdb.Type == CommanderTypes.Intelligence)
                    count++;
            return count;
        }

        /// <summary>Recruit one operative onto the faction: roll a competence ceiling, create the agent entity, and
        /// stamp its tradecraft bonus (so the agent has a real skill the detection roll reads). Pure of the schedule.</summary>
        public static Entity RecruitAgent(Entity directorateHost, Game game)
        {
            var agentDB = CommanderFactory.CreateAgent(game);
            agentDB.CommissionedOn = directorateHost.StarSysDateTime.Date;
            agentDB.RankedOn = directorateHost.StarSysDateTime.Date;

            // Roll the recruit's ceiling on the same 0–200 bell curve the naval academy uses (mean 100).
            var generator = new GaussianRandom();
            agentDB.ExperienceCap = generator.NextBellCurve(game.RNG, 0, 200, 100, 33.333);

            var agentEntity = CommanderFactory.Create(directorateHost.Manager, directorateHost.FactionOwnerID, agentDB);

            // Stamp the tradecraft bonus scaled by the ceiling — the skill CovertRisk.Resolve will read in E3.
            if (agentEntity.TryGetDataBlob<BonusesDB>(out var bonusesDB))
                foreach (var bonus in CommanderBonuses.RollEspionageCompetence(agentDB.ExperienceCap))
                    bonusesDB.Bonuses.Add(bonus);

            return agentEntity;
        }
    }
}
