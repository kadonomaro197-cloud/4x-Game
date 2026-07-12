using System;
using Pulsar4X.Interfaces;
using Pulsar4X.Components;
using Pulsar4X.Engine;
using Pulsar4X.Datablobs;

namespace Pulsar4X.People
{
    /// <summary>
    /// Exploration X.0-2 — the component that MAKES a research academy buildable, the scientist twin of
    /// <see cref="NavalAcademyAtb"/>. Installing a Research Academy on a colony enrols a class (seeding a
    /// <see cref="ResearchAcademyDB"/> if the colony has none) and schedules <see cref="ResearchAcademyProcessor"/> to
    /// graduate it. <see cref="SpecialtyCategory"/> — the tech category the school trains its scientists in — is chosen
    /// in the designer (a <c>GuiTechCategorySelection</c> property) and carried into the graduate's rolled research
    /// competence. This is the reach half of cradle-to-grave: mineral → material → this component → the build decision.
    /// </summary>
    public class ResearchAcademyAtb : IComponentDesignAttribute
    {
        public int ClassSize { get; internal set; }
        public int TrainingPeriodInMonths { get; internal set; }
        public string SpecialtyCategory { get; internal set; }

        private ResearchAcademy _academy;

        public ResearchAcademyAtb(double classSize, double period, string specialtyCategory)
        {
            ClassSize = (int)classSize;
            TrainingPeriodInMonths = (int)period;
            SpecialtyCategory = specialtyCategory;
        }

        public ResearchAcademyAtb(int classSize, int period, string specialtyCategory)
        {
            ClassSize = classSize;
            TrainingPeriodInMonths = period;
            SpecialtyCategory = specialtyCategory;
        }

        public ResearchAcademyAtb(ResearchAcademyAtb db)
        {
            ClassSize = db.ClassSize;
            TrainingPeriodInMonths = db.TrainingPeriodInMonths;
            SpecialtyCategory = db.SpecialtyCategory;
        }

        public string AtbDescription()
        {
            return "Class Size: " + ClassSize.ToString()
                + "\nTraining Length: " + TrainingPeriodInMonths.ToString() + " months"
                + "\nSpecialty: " + SpecialtyCategory;
        }

        public string AtbName()
        {
            return "Research Academy";
        }

        public void OnComponentInstallation(Entity parentEntity, ComponentInstance componentInstance)
        {
            DateTime graduationDate = parentEntity.StarSysDateTime + TimeSpan.FromDays(TrainingPeriodInMonths * 30);

            _academy = new ResearchAcademy() {
                ClassSize = this.ClassSize,
                GraduationDate = graduationDate,
                TrainingPeriodInMonths = this.TrainingPeriodInMonths,
                SpecialtyCategory = this.SpecialtyCategory
            };

            if (parentEntity.TryGetDataBlob<ResearchAcademyDB>(out var academyDB))
            {
                academyDB.Academies.Add(_academy);
            }
            else
            {
                academyDB = new ResearchAcademyDB();
                academyDB.Academies.Add(_academy);
                parentEntity.SetDataBlob(academyDB);
            }
            parentEntity.Manager.ManagerSubpulses.AddEntityInterupt(graduationDate, nameof(ResearchAcademyProcessor), parentEntity);
        }

        public void OnComponentUninstallation(Entity parentEntity, ComponentInstance componentInstance)
        {
            if (parentEntity.TryGetDataBlob<ResearchAcademyDB>(out var academyDB))
            {
                academyDB.Academies.Remove(_academy);

                if (academyDB.Academies.Count == 0)
                {
                    parentEntity.RemoveDataBlob<ResearchAcademyDB>();
                }
            }
        }
    }
}
