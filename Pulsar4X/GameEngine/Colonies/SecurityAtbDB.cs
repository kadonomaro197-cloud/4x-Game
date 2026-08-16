using Newtonsoft.Json;
using Pulsar4X.Interfaces;
using Pulsar4X.Components;
using Pulsar4X.Engine;
using Pulsar4X.Datablobs;

namespace Pulsar4X.Colonies
{
    /// <summary>
    /// A component design attribute: SECURITY — the ORDER/legitimacy side of the civic loop (the Security option of the
    /// Civic door, docs/assembler/01-IO-civic.md §A.1/§B, whose "stability effect" reads "-N unrest"). A colony builds
    /// security institutions (precincts) that keep a restless province orderly; their combined
    /// <see cref="SecurityRating"/> is a POSITIVE term in <see cref="LegitimacyProcessor"/> — the designer's
    /// "reduce unrest" expressed as legitimacy points (unrest is the inverse of legitimacy, the rebellion trigger).
    ///
    /// One dial:
    ///  • <see cref="SecurityRating"/> — the order strength this installation provides. Summed across the colony's
    ///    precincts (health-scaled) and added to the province's legitimacy, capped by
    ///    <see cref="LegitimacyDB.MaxSecurityBonus"/>, so building precincts can hold a province above the
    ///    collapse/rebellion band (<see cref="LegitimacyDB.CollapseThreshold"/>). A bombarded precinct provides less
    ///    order (the grave rung); destroying it drops the total and legitimacy falls.
    ///
    /// Wired behind <see cref="LegitimacyProcessor.EnableSecurityLegitimacy"/> (default OFF → byte-identical), so the
    /// existing suite is unchanged until a menu game / test opts in — the sibling of the food/employment civic terms.
    /// Host-agnostic (colony or station). Summed on demand, so install/uninstall need no bookkeeping (the
    /// <see cref="FoodProductionAtbDB"/> pattern). Cradle-to-grave: designed in the component designer (Civic ▸ Security)
    /// → built from materials at a colony → raises legitimacy → destroyed (bombardment) drops it.
    /// </summary>
    public class SecurityAtbDB : BaseDataBlob, IComponentDesignAttribute
    {
        /// <summary>The order strength this installation provides (legitimacy points, before the colony-wide cap).
        /// Summed across the colony's precincts, health-scaled, into the legitimacy security term.</summary>
        [JsonProperty] public double SecurityRating { get; internal set; }

        public SecurityAtbDB() { }

        public SecurityAtbDB(double securityRating)
        {
            SecurityRating = securityRating < 0 ? 0 : securityRating;
        }

        public override object Clone() => new SecurityAtbDB(SecurityRating);

        public void OnComponentInstallation(Entity parentEntity, ComponentInstance component) { }

        public void OnComponentUninstallation(Entity parentEntity, ComponentInstance componentInstance) { }

        public string AtbName() => "Security";

        public string AtbDescription() => "Provides order — raises a restless province's legitimacy, holding off rebellion.";
    }
}
