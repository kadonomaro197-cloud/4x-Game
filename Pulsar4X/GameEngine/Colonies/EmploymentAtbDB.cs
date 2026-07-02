using Newtonsoft.Json;
using Pulsar4X.Interfaces;
using Pulsar4X.Components;
using Pulsar4X.Engine;
using Pulsar4X.Datablobs;

namespace Pulsar4X.Colonies
{
    /// <summary>
    /// A component design attribute: this installation provides N jobs (worker slots). Total jobs on a host =
    /// sum across all installed components carrying this attribute (see ComponentInstancesDBExtensions.GetTotalJobs).
    ///
    /// Morale input (M2, docs/MORALE-AND-POPULATION-DESIGN.md): jobs vs population is TWO-SIDED — population
    /// above jobs is unemployment (morale debuff); jobs covered is full employment (buff). The hard people-draw
    /// (M3) will actually pull workers from the population tank to staff these.
    /// </summary>
    public class EmploymentAtbDB : BaseDataBlob, IComponentDesignAttribute
    {
        [JsonProperty]
        public int Jobs { get; internal set; }

        public EmploymentAtbDB() { }

        public EmploymentAtbDB(double jobs) : this((int)jobs) { }

        public EmploymentAtbDB(int jobs)
        {
            Jobs = jobs;
        }

        public override object Clone() => new EmploymentAtbDB(Jobs);

        public void OnComponentInstallation(Entity ship, ComponentInstance component) { }

        public void OnComponentUninstallation(Entity parentEntity, ComponentInstance componentInstance) { }

        public string AtbName() => "Employment";

        public string AtbDescription() => "Jobs (worker slots) provided by this installation.";
    }
}
