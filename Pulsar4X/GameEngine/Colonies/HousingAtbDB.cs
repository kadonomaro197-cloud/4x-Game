using Newtonsoft.Json;
using Pulsar4X.Interfaces;
using Pulsar4X.Components;
using Pulsar4X.Engine;
using Pulsar4X.Datablobs;

namespace Pulsar4X.Colonies
{
    /// <summary>
    /// A component design attribute: quality housing — a morale COMFORT bonus. This is the "tier" layer on top
    /// of bare life-support capacity: <see cref="Pulsar4X.Galaxy.PopulationSupportAtbDB"/> keeps colonists
    /// ALIVE (raises the population ceiling); HousingAtbDB keeps them CONTENT (comfort → morale). That split is
    /// what makes designing a nicer housing complex worth it instead of just stamping minimal habitats.
    ///
    /// Total comfort on a host = sum across installed components (see
    /// ComponentInstancesDBExtensions.GetHousingComfort). Morale input (M2, docs/MORALE-AND-POPULATION-DESIGN.md).
    /// </summary>
    public class HousingAtbDB : BaseDataBlob, IComponentDesignAttribute
    {
        [JsonProperty]
        public double Comfort { get; internal set; }

        public HousingAtbDB() { }

        public HousingAtbDB(double comfort)
        {
            Comfort = comfort;
        }

        public override object Clone() => new HousingAtbDB(Comfort);

        public void OnComponentInstallation(Entity ship, ComponentInstance component) { }

        public void OnComponentUninstallation(Entity parentEntity, ComponentInstance componentInstance) { }

        public string AtbName() => "Housing";

        public string AtbDescription() => "Living-quality comfort (raises morale).";
    }
}
