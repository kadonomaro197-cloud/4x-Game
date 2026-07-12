using System;
using Newtonsoft.Json;
using Pulsar4X.Components;
using Pulsar4X.Engine;
using Pulsar4X.Interfaces;

namespace Pulsar4X.Factions
{
    /// <summary>
    /// Espionage E1 — the component that MAKES a spy network buildable, the espionage twin of
    /// <see cref="Pulsar4X.GeoSurveys.GeoSurveyAtb"/> / <see cref="Pulsar4X.People.ResearchAcademyAtb"/>. Installing an
    /// Intelligence Directorate on a colony seeds (or grows) an <see cref="IntelDirectorateDB"/> that projects the
    /// faction's covert-op capacity + counter-intelligence rating. This is the reach half of espionage's
    /// cradle-to-grave: mineral → material → THIS component → the build decision → (later) the covert op it funds; and
    /// uninstalling/destroying it withdraws that capacity (the grave rung — a faction whose directorates are all
    /// destroyed can run no ops and has no counter-intel shield).
    /// </summary>
    public class IntelDirectorateAtb : IComponentDesignAttribute
    {
        // [JsonProperty] + parameterless ctor: this atb is serialized INSIDE the intelligence-directorate ComponentDesign
        // (the design is stored on the colony), so Game.Load needs a default ctor to deserialize it — without one it
        // throws "Unable to find a constructor to use" (the ResearchAcademyAtb save/load lesson, gotcha #10).
        [JsonProperty] public int OpCapacity { get; internal set; }
        [JsonProperty] public int CounterIntelRating { get; internal set; }

        public IntelDirectorateAtb() { }

        public IntelDirectorateAtb(double opCapacity, double counterIntelRating)
        {
            OpCapacity = (int)opCapacity;
            CounterIntelRating = (int)counterIntelRating;
        }

        public IntelDirectorateAtb(IntelDirectorateAtb other)
        {
            OpCapacity = other.OpCapacity;
            CounterIntelRating = other.CounterIntelRating;
        }

        public void OnComponentInstallation(Entity parentEntity, ComponentInstance componentInstance)
        {
            if (parentEntity.TryGetDataBlob<IntelDirectorateDB>(out var directorate))
            {
                directorate.OpCapacity += OpCapacity;
                directorate.CounterIntelRating += CounterIntelRating;
            }
            else
            {
                parentEntity.SetDataBlob(new IntelDirectorateDB
                {
                    OpCapacity = OpCapacity,
                    CounterIntelRating = CounterIntelRating
                });

                // First directorate on this colony → start the recruiting cadence (E2). Scheduled only on the FIRST
                // install so extra directorates raise capacity without stacking duplicate recruiting timers. The
                // processor reschedules itself; it stops when the directorate is gone (the grave rung).
                DateTime firstRecruit = parentEntity.StarSysDateTime + TimeSpan.FromDays(IntelDirectorateProcessor.RecruitIntervalDays);
                parentEntity.Manager.ManagerSubpulses.AddEntityInterupt(firstRecruit, nameof(IntelDirectorateProcessor), parentEntity);
            }
        }

        public void OnComponentUninstallation(Entity parentEntity, ComponentInstance componentInstance)
        {
            if (parentEntity.TryGetDataBlob<IntelDirectorateDB>(out var directorate))
            {
                directorate.OpCapacity -= OpCapacity;
                directorate.CounterIntelRating -= CounterIntelRating;

                // The last directorate torn down leaves no spy capacity — drop the blob so the colony reads "no network".
                if (directorate.OpCapacity <= 0 && directorate.CounterIntelRating <= 0)
                    parentEntity.RemoveDataBlob<IntelDirectorateDB>();
            }
        }

        public string AtbName() => "Intelligence Directorate";

        public string AtbDescription() =>
            "Concurrent covert ops: " + OpCapacity +
            "\nCounter-intelligence rating: " + CounterIntelRating;
    }
}
