using System;
using Pulsar4X.Datablobs;
using Pulsar4X.DataStructures;

namespace Pulsar4X.People
{
    /// <summary>Exploration X.0 — one class enrolled at a RESEARCH ACADEMY. The scientist twin of
    /// <see cref="NavalAcademy"/>: a school of a given QUALITY (training length) that graduates scientists trained in a
    /// tech <see cref="SpecialtyCategory"/>.</summary>
    public struct ResearchAcademy
    {
        public int ClassSize;
        public DateTime GraduationDate;
        public int TrainingPeriodInMonths;
        /// <summary>The tech category the school trains its scientists in (their rolled research competence lands here).</summary>
        public string SpecialtyCategory;
    }

    /// <summary>
    /// Exploration X.0 — the RESEARCH ACADEMY roster on a colony/station, the scientist mirror of
    /// <see cref="NavalAcademyDB"/>. Holds the enrolled classes; <see cref="Pulsar4X.People.ResearchAcademyProcessor"/>
    /// graduates each on its date. Byte-identical until a component carries a <c>ResearchAcademyAtb</c> that adds one.
    /// </summary>
    public class ResearchAcademyDB : BaseDataBlob
    {
        public SafeList<ResearchAcademy> Academies = new SafeList<ResearchAcademy>();

        public ResearchAcademyDB() { }

        public ResearchAcademyDB(ResearchAcademyDB db)
        {
            Academies = new SafeList<ResearchAcademy>(db.Academies);
        }

        public override object Clone()
        {
            return new ResearchAcademyDB(this);
        }
    }
}
