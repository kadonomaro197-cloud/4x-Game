using Newtonsoft.Json;
using Pulsar4X.Interfaces;
using Pulsar4X.Components;
using Pulsar4X.Engine;
using Pulsar4X.Datablobs;

namespace Pulsar4X.Colonies
{
    /// <summary>
    /// A component design attribute: AMENITY — the RECREATION side of the civic loop (the Recreation / civic-amenity
    /// option of the Civic door, docs/Actual HTMLs Of designers/civicderived.html, which the engine previously LACKED —
    /// it was one of the door's mock-only jobs, "shelved / out of scope" until now). Parks, arenas, theatres and
    /// leisure services that make a colony a nicer place to live: their combined <see cref="AmenityRating"/> is a
    /// POSITIVE morale term in <see cref="ColonyMoraleDB.ComputeMorale"/> — people are content where there is something
    /// to do — a sibling of the health-care (<see cref="MedicalAtbDB"/>) and food-quality bonuses, and a NEW, distinct
    /// morale input (leisure, not housing comfort and not medicine).
    ///
    /// One dial:
    ///  • <see cref="AmenityRating"/> — the leisure strength this installation provides. Summed across the colony's
    ///    recreation buildings (health-scaled) and added to morale, capped by
    ///    <see cref="ColonyMoraleDB.MaxAmenityBonus"/>, so building amenities lifts a struggling colony's morale. A
    ///    bombarded arena provides less leisure (the grave rung); destroying it drops the total and morale falls.
    ///
    /// Wired behind <see cref="PopulationProcessor.EnableAmenityMorale"/> (default OFF → byte-identical), so the
    /// existing suite is unchanged until a menu game / test opts in — the exact sibling of the medical / security /
    /// food / employment civic terms. Host-agnostic (colony or station). Summed on demand, so install/uninstall need no
    /// bookkeeping (the <see cref="MedicalAtbDB"/> pattern). Cradle-to-grave: designed in the component designer
    /// (Civic ▸ Development) → built from materials at a colony → lifts morale → destroyed (bombardment) drops it.
    /// </summary>
    public class AmenityAtbDB : BaseDataBlob, IComponentDesignAttribute
    {
        /// <summary>The leisure strength this installation provides (morale points, before the colony-wide cap).
        /// Summed across the colony's recreation buildings, health-scaled, into the morale amenity term.</summary>
        [JsonProperty] public double AmenityRating { get; internal set; }

        public AmenityAtbDB() { }

        public AmenityAtbDB(double amenityRating)
        {
            AmenityRating = amenityRating < 0 ? 0 : amenityRating;
        }

        public override object Clone() => new AmenityAtbDB(AmenityRating);

        public void OnComponentInstallation(Entity parentEntity, ComponentInstance component) { }

        public void OnComponentUninstallation(Entity parentEntity, ComponentInstance componentInstance) { }

        public string AtbName() => "Amenity";

        public string AtbDescription() => "Provides recreation and leisure — lifts a colony's morale (people are content where there is something to do).";
    }
}
