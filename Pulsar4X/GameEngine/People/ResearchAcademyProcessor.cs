using System;
using System.Collections.Generic;
using System.Linq;
using Pulsar4X.Engine;
using Pulsar4X.Interfaces;

namespace Pulsar4X.People
{
    /// <summary>
    /// Exploration X.0 — the RESEARCH ACADEMY graduation, the scientist twin of <see cref="NavalAcademyProcessor"/>.
    /// When a class is due, it graduates <c>ClassSize</c> SCIENTIST entities whose RESEARCH COMPETENCE is rolled from
    /// the school's QUALITY (training length) in the academy's specialty tech category — the "build a research academy"
    /// decision the developer chose: a better school (longer program) produces higher-ceiling scientists, and each
    /// graduate then earns experience over time. The rolled competence lands on the graduate's <c>BonusesDB</c>, which
    /// <c>ResearchProcessor.RefreshPointModifiers</c> folds into research output once the scientist is assigned to a
    /// lab (<c>AssignScientistOrder</c>) — the same generate→read loop the officer path uses. Byte-identical until a
    /// component carries <c>ResearchAcademyAtb</c> (X.0-2) and schedules this processor.
    /// </summary>
    public class ResearchAcademyProcessor : IInstanceProcessor
    {
        internal override void ProcessEntity(Entity entity, DateTime atDateTime)
        {
            if (!entity.TryGetDataBlob<ResearchAcademyDB>(out var academyDB)) return;

            // Defensive (unobserved-throw-on-sim-thread class): a stale/duplicate interrupt, or two classes sharing a
            // graduation date, can leave this filter empty. .First() would throw InvalidOperationException and freeze
            // the clock; guard with .Any() and graduate nothing this fire instead. (ResearchAcademy is a struct, so
            // FirstOrDefault can't be null-tested.)
            var matchingClasses = academyDB.Academies.Where(a => a.GraduationDate.Date == atDateTime.Date);
            if (!matchingClasses.Any()) return;
            var academy = matchingClasses.First();

            // Graduate the class.
            for (int i = 0; i < academy.ClassSize; i++)
                GraduateScientist(entity, academy.TrainingPeriodInMonths, academy.SpecialtyCategory);

            // Remove the graduated class and enrol the next one, then schedule its graduation.
            academyDB.Academies.Remove(academy);
            var graduationDate = academy.GraduationDate + TimeSpan.FromDays(academy.TrainingPeriodInMonths * 30);
            academyDB.Academies.Add(new ResearchAcademy() {
                ClassSize = academy.ClassSize,
                GraduationDate = graduationDate,
                TrainingPeriodInMonths = academy.TrainingPeriodInMonths,
                SpecialtyCategory = academy.SpecialtyCategory
            });
            entity.Manager.ManagerSubpulses.AddEntityInterupt(graduationDate, nameof(ResearchAcademyProcessor), entity);
        }

        /// <summary>
        /// Graduate ONE scientist entity from a school of the given quality, with research competence in the specialty
        /// rolled onto its <c>BonusesDB</c> — the exact mirror of the officer graduation in
        /// <see cref="NavalAcademyProcessor"/>: the training length shifts the graduate's <c>ExperienceCap</c> ceiling
        /// on a bell curve (a better school → a higher cap), and <see cref="CommanderBonuses.RollResearchCompetence"/>
        /// turns that cap into the competence bonus (in the academy's category). Starting experience (0–30) is seeded
        /// like an officer, so a graduate can grow further over time. Returns the graduate entity (so a gauge can read
        /// it). Static/no scheduling → directly testable.
        /// </summary>
        internal static Entity GraduateScientist(Entity hostEntity, int trainingPeriodInMonths, string specialtyCategory)
        {
            var generator = new GaussianRandom();
            var scientistDB = CommanderFactory.CreateScientist(hostEntity.Manager.Game);

            scientistDB.CommissionedOn = hostEntity.StarSysDateTime.Date;
            scientistDB.RankedOn = hostEntity.StarSysDateTime.Date;

            // Quality of the school sets the graduate's ceiling — longer training bell-curves toward a higher cap
            // (the same 0–200 curve the naval academy uses: 1 month → mean 77, 48 months → mean 124).
            double mean = 100 + (trainingPeriodInMonths - 24);
            scientistDB.ExperienceCap = generator.NextBellCurve(hostEntity.Manager.Game.RNG, 0, 200, mean, 33.333);

            // Seed a little starting experience for graduates with potential (they earn more over time).
            if (scientistDB.ExperienceCap > 30)
            {
                double mu = 1 + (trainingPeriodInMonths / 48.0) * 27;
                double sigma = mu / 6.0;
                scientistDB.Experience = generator.NextBellCurve(hostEntity.Manager.Game.RNG, 0, Math.Min(scientistDB.ExperienceCap, 30), mu, sigma);
            }
            else
            {
                scientistDB.Experience = 0;
            }

            var scientistEntity = CommanderFactory.Create(hostEntity.Manager, hostEntity.FactionOwnerID, scientistDB);

            // X.0 GENERATOR: turn the graduate's competence into a research bonus on their BonusesDB, scaled by the
            // rolled ExperienceCap, in the academy's specialty — the scientist twin of RollCombatCompetence.
            if (scientistEntity.TryGetDataBlob<BonusesDB>(out var bonusesDB) && !string.IsNullOrEmpty(specialtyCategory))
            {
                foreach (var bonus in CommanderBonuses.RollResearchCompetence((int)scientistDB.ExperienceCap, specialtyCategory))
                    bonusesDB.Bonuses.Add(bonus);
            }

            return scientistEntity;
        }
    }
}
