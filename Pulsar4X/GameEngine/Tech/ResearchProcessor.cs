using System;
using Pulsar4X.Interfaces;
using Pulsar4X.Factions;
using Pulsar4X.Engine;
using Pulsar4X.Events;
using System.IO;
using Pulsar4X.People;
using Pulsar4X.Names;

namespace Pulsar4X.Technology
{
    /// <summary>
    /// See also the Installation Processors for DoResearch
    /// </summary>
    public class ResearchProcessor : IHotloopProcessor
    {
        public TimeSpan RunFrequency => TimeSpan.FromDays(1);

        public TimeSpan FirstRunOffset => TimeSpan.FromHours(0.5);

        public Type GetParameterType => typeof(ResearcherDB);

        /// <summary>X.0-3 tuning dial: EXPERIENCE a scientist earns per month of working a lab (Experience is 0–200).
        /// Modest, so a scientist takes years to approach their school-set ceiling — a career, not an overnight jump.</summary>
        public const int ExperienceGainPerMonth = 2;

        private Game _game;

        public void Init(Game game)
        {
            _game = game;
            EventManager.Instance.Subscribe(EventType.TechnologyQueued, OnTechnologyChanged);
            EventManager.Instance.Subscribe(EventType.TechnologyRemovedFromQueue, OnTechnologyChanged);
            EventManager.Instance.Subscribe(EventType.TechnologyMovedInQueue, OnTechnologyChanged);
            EventManager.Instance.Subscribe(EventType.TechnologyFundingChanged, OnTechnologyChanged);
            EventManager.Instance.Subscribe(EventType.TechnologyLabScientistAssigned, OnTechnologyChanged);
            EventManager.Instance.Subscribe(EventType.TechnologyLabScientistUnassigned, OnTechnologyChanged);
        }

        public void ProcessEntity(Entity entity, int deltaSeconds)
        {
            DoResearch(entity);
        }

        public int ProcessManager(EntityManager manager, int deltaSeconds)
        {
            var entitysWithResearch = manager.GetAllEntitiesWithDataBlob<ResearcherDB>();
            foreach(var entity in entitysWithResearch)
            {
                ProcessEntity(entity, deltaSeconds);
            }

            return entitysWithResearch.Count;
        }

        /// <summary>
        /// adds research points to a scientists project.
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="factionAbilities"></param>
        /// <param name="factionTechs"></param>
        internal void DoResearch(Entity entity)
        {
            Entity faction = entity.Manager.Game.Factions[entity.FactionOwnerID];

            if(!faction.TryGetDataBlob<FactionInfoDB>(out var factionInfoDB))
                return;

            FactionDataStore factionDataStore = factionInfoDB.Data;

            // If unable to get the db return
            if(!entity.TryGetDataBlob<ResearcherDB>(out var researcherDB))
                return;

            // Check if queue is empty
            if(!researcherDB.TechQueue.TryPeek(out var techId))
                return;

            // Get the tech that is being researched
            var tech = factionDataStore.Techs[techId];

            // Make sure that the tech is researchable
            if(!factionDataStore.IsResearchable(tech.UniqueID))
            {
                // If it isn't, dequeue the tech and return
                researcherDB.TechQueue.TryDequeue(out var result);
                return;
            }

            // Get the calculated total number of points to add
            var pointsToAdd = researcherDB.PointsPerDay.GetValue();

            // Government MODULATOR (#30): an OPEN society races ahead, a CLOSED one drags (ResearchMultiplier).
            // ×1.0 at the default Mid openness, so this changes nothing until a non-Mid regime is set.
            pointsToAdd = (int)(pointsToAdd * GovernmentTools.Of(faction).ResearchMultiplier());

            // Make sure the calculated total is > 0
            if(pointsToAdd <= 0)
                return;

            var cost = researcherDB.CostPerDay.GetValue();

            // Check to make sure the cost can be paid
            if(factionInfoDB.Money.GetCurrentFunds() < cost)
                return;

            // Pay the costs
            factionInfoDB.Money.AddExpense(
                entity.Manager.StarSysDateTime,
                TransactionCategory.Research,
                $"Payment to run research lab on {entity.Manager.StarSysDateTime.ToShortDateString()}",
                cost);

            // Apply the research points
            int currentLvl = tech.Level;
            factionDataStore.AddTechPoints(tech, pointsToAdd);

            // If the tech level increased the tech research completed
            if (tech.Level > currentLvl)
            {
                // Remove the current tech from the queue
                if(!researcherDB.TechQueue.TryDequeue(out var result))
                    throw new Exception("Unable to dequeue from tech queue");

                if (tech.Faction != null && tech.Design != null && tech.Faction.TryGetDataBlob<FactionInfoDB>(out var factionInfo))
                {
                    factionInfo.IndustryDesigns[tech.UniqueID] = tech.Design;
                }

                // Sync any materials this tech unlocked — Unlock() already moved them to CargoGoods
                // but IndustryDesigns is only populated at startup via SetIndustryDesigns().
                if (tech.Unlocks.TryGetValue(tech.Level, out var unlockedIds))
                {
                    foreach (var unlockId in unlockedIds)
                    {
                        if (factionDataStore.CargoGoods.IsMaterial(unlockId))
                            factionInfoDB.IndustryDesigns[unlockId] = (IConstructableDesign)factionDataStore.CargoGoods[unlockId];
                    }
                }

                // if (cycleProject)
                //     scientist.ProjectQueue.Add((project.UniqueID, true));

                // Publish an event for research completion
                EventManager.Instance.Publish(
                    Event.Create(
                        EventType.ResearchCompleted,
                        entity.StarSysDateTime,
                        $"{tech.Name} research completed!",
                        entity.FactionOwnerID,
                        entity.Manager.ManagerID,
                        entity.Id));
            }

            // X.0-3: a scientist actually working a lab earns EXPERIENCE over time, deepening their competence in the
            // category they research (the "bonus on top" of the school-set graduation floor). Byte-identical for the
            // default start: the legacy scientist path leaves ScientistId == -1, so no assigned-entity scientist exists
            // and nothing grows. Monthly cadence keeps it a slow career climb rather than a daily ratchet.
            if (researcherDB.ScientistId >= 0
                && entity.StarSysDateTime.Day == 1
                && entity.Manager.TryGetGlobalEntityById(researcherDB.ScientistId, out var scientistEntity))
            {
                if (GrowScientistExperience(scientistEntity, tech.Category, ExperienceGainPerMonth))
                    RefreshPointModifiers(researcherDB, tech, scientistEntity);
            }
        }

        /// <summary>
        /// Exploration X.0-3 — grow an assigned scientist's EXPERIENCE toward its (school-set) cap and refresh the
        /// experience-based research bonus in the category they are working. The "bonus on top" of
        /// ceiling-now-plus-bonus: the graduation "Research Aptitude" competence (rolled from the CAP) is left
        /// untouched; this adds/updates a SEPARATE growing bonus keyed on CURRENT Experience. Experience never exceeds
        /// ExperienceCap — the school still bounds a scientist's ultimate ceiling. Returns true iff experience actually
        /// rose (so the caller re-folds PointsPerDay). Pure/testable — no clock, no scheduling.
        /// </summary>
        internal static bool GrowScientistExperience(Entity scientist, string techCategory, int gain)
        {
            if (gain <= 0 || string.IsNullOrEmpty(techCategory))
                return false;
            if (!scientist.TryGetDataBlob<CommanderDB>(out var commanderDB))
                return false;
            if (commanderDB.Experience >= commanderDB.ExperienceCap)
                return false;   // already at the school-set ceiling — a career can't exceed the school's potential

            commanderDB.Experience = Math.Min(commanderDB.ExperienceCap, commanderDB.Experience + gain);

            // Replace ONLY the experience bonus in this category; the graduation "Research Aptitude" bonus is left alone.
            if (scientist.TryGetDataBlob<BonusesDB>(out var bonusesDB))
            {
                bonusesDB.Bonuses.RemoveAll(b =>
                    b.Name == CommanderBonuses.ResearchExperienceBonusName && b.FilterId == techCategory);
                foreach (var bonus in CommanderBonuses.RollResearchExperienceBonus(commanderDB.Experience, techCategory))
                    bonusesDB.Bonuses.Add(bonus);
            }
            return true;
        }

        private void OnTechnologyChanged(Event e)
        {
            // Recalculate the stats of the researchDB
            var system = _game.Systems.Find(s => s.ManagerID.Equals(e.SystemId));

            if(system == null)
                return;

            if(e.EntityId == null || e.FactionId == null)
                return;

            if(!system.TryGetEntityById((int)e.EntityId, out var labEntity))
                return;

            if(!labEntity.TryGetDataBlob<ResearcherDB>(out var researcherDB))
                return;

            // Try to find the tech at the front of the queue
            Tech? tech = null;
            if(researcherDB.TechQueue.TryPeek(out var techId))
            {
                if(_game.Factions[(int)e.FactionId].TryGetDataBlob<FactionInfoDB>(out var factionInfoDB))
                {
                    tech = factionInfoDB.Data.Techs[techId];
                }
            }

            Entity? scientist = null;
            if(researcherDB.ScientistId >= 0)
            {
                if(system.TryGetGlobalEntityById(researcherDB.ScientistId, out var scientistEntity))
                {
                    scientist = scientistEntity;
                }
            }

            RefreshCostModifiers(researcherDB);
            RefreshPointModifiers(researcherDB, tech, scientist);
        }

        public static void RefreshCostModifiers(ResearcherDB researcherDB)
        {
            // TODO: Add bonuses for corporation administration

            // Remove any previous cost modifier
            researcherDB.CostPerDay.RemoveModifier(researcherDB.FundingCostModifierId);

            researcherDB.FundingCostModifierId = "funding-cost-modifier";

            decimal fundingModifier = GetFundingCostModifier(researcherDB.FundingLevel);

            if(fundingModifier != 1)
            {
                // Add new modifier
                researcherDB.CostPerDay.AddModifier(
                    new Modifier<decimal>(
                        researcherDB.FundingCostModifierId,
                        "Funding Cost Modifier",
                        fundingModifier,
                        (current, multiplier) => current * multiplier,
                        1.0f
                    )
                );
            }
        }

        public static decimal GetFundingCostModifier(byte level)
        {
            return level switch
            {
                0 => 0,
                1 => 1,
                2 => 3,
                3 => 7,
                4 => 13,
                5 => 22,
                _ => throw new InvalidDataException("Unable to determine funding modifier")
            };
        }

        public static int GetFundingPointModifier(byte level)
        {
            return level switch
            {
                0 => 0,
                1 => 1,
                2 => 2,
                3 => 3,
                4 => 4,
                5 => 5,
                _ => throw new InvalidDataException("Unable to determine funding modifier")
            };
        }

        public static void RefreshPointModifiers(ResearcherDB researcherDB, Tech? currentTech, Entity? scientist)
        {
            // clear all modifiers, this may not be the best way to handle this?
            researcherDB.PointsPerDay.ClearAllModifiers();


            researcherDB.FundingPointModifierId = "funding-point-modifier";

            // Add new modifier
            int fundingMultiplier = GetFundingPointModifier(researcherDB.FundingLevel);

            if(fundingMultiplier != 1)
            {
                researcherDB.PointsPerDay.AddModifier(
                    new Modifier<int>(
                        researcherDB.FundingPointModifierId,
                        $"Funding Modifier",
                        fundingMultiplier,
                        (current, multiplier) => current * multiplier,
                        1.0f
                    )
                );
            }

            if(currentTech != null)
            {
                // Apply any category bonuses
                foreach(var (category, bonus) in researcherDB.BonusCategories)
                {
                    // Make sure the categories match
                    if(!currentTech.Category.Equals(category))
                        continue;

                    // Add in the modifier as a percentage increase
                    researcherDB.PointsPerDay.AddModifier(
                        new Modifier<int>(
                            category,
                            $"{bonus * 100}% {researcherDB.OwningEntity.Manager.Game.StartingGameData.TechCategories[category].Name} Category Bonus",
                            (int)(bonus * 100),
                            (current, multiplier) => current + (current * multiplier / 100),
                            2.0f
                        )
                    );
                }
            }

            if(scientist != null && currentTech != null)
            {
                // Apply any scientist bonuses
                if(scientist.TryGetDataBlob<BonusesDB>(out var bonusesDB))
                {
                    var name = scientist.TryGetDataBlob<NameDB>(out var nameDB) ? nameDB.DefaultName : "Unknown Scientist";

                    foreach(var bonus in bonusesDB.Bonuses)
                    {
                        // Make sure the categories match
                        if(!currentTech.Category.Equals(bonus.FilterId))
                            continue;

                        if(bonus.Type == BonusType.Number)
                        {
                            // Add in the modifier as a flat increase
                            researcherDB.PointsPerDay.AddModifier(
                                new Modifier<int>(
                                    bonus.Name,
                                    $"{bonus.Value} {name} {bonus.Name}",
                                    (int)bonus.Value,
                                    (current, multiplier) => current + multiplier,
                                    3.0f
                                )
                            );
                        }
                        else if(bonus.Type == BonusType.Perentage)
                        {
                            // Add in the modifier as a percentage increase
                            researcherDB.PointsPerDay.AddModifier(
                                new Modifier<int>(
                                    bonus.Name,
                                    $"{bonus.Value * 100}% from {name}",
                                    (int)(bonus.Value * 100),
                                    (current, multiplier) => current + (current * multiplier / 100),
                                    3.0f
                                )
                            );
                        }
                    }
                }
            }
        }

        /// <summary>
        /// assigns more labs to a given scientist
        /// will not assign more than scientists MaxLabs
        /// </summary>
        /// <param name="scientist"></param>
        /// <param name="labs"></param>
        public static void AssignLabs(Scientist scientist, byte labs)
        {
            //TODO: ensure that the labs are availible to assign.
            scientist.AssignedLabs = Math.Min(scientist.MaxLabs, labs);
        }

        public static void AddLabs(Scientist scientist, int labs)
        {
            //TODO: ensure that the labs are availible to assign.
            byte numlabs = (byte)(scientist.AssignedLabs + labs);
            AssignLabs(scientist, numlabs);
        }



        /// <summary>
        /// adds a tech to a scientists research queue.
        /// </summary>
        /// <param name="scientist"></param>
        /// <param name="techID"></param>
        public static void AssignProject(Scientist scientist, string techID)
        {
            //TODO: check valid research, scientist etc for the empire.
            //TechSD project = _game.StaticData.Techs[techID];
            scientist.ProjectQueue.Add((techID, false));
        }

        public static void AssignTech(ResearcherDB researcherDB, string techId)
        {
            researcherDB.TechQueue.Enqueue(techId);
        }
    }
}
