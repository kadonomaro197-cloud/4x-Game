using Newtonsoft.Json;
using Pulsar4X.Engine;
using Pulsar4X.Components;

namespace Pulsar4X.Construction
{
    /// <summary>
    /// A FIELD CONSTRUCTOR — the ability that lets a unit ASSEMBLE a designed buildable (a station today; a building
    /// later) ON SITE out of components it (or its fleet) is carrying in cargo. This is the developer's model "A": you
    /// build the pieces at a factory, pack them in a hold, fly a constructor out, and it puts them together where you
    /// want them — a star, a belt, a Lagrange point, a planet's orbit.
    ///
    /// It is a COMPONENT (CONVENTIONS §6) — researched → built → mounted on a ship → lost when the ship dies — so the
    /// capability is reachable and losable through the normal chain, not a bespoke engine flag. It is INERT on install:
    /// the <see cref="OnSiteConstructionOrder"/> reads it off the ship at build time (via
    /// <c>TryGetComponentsByAttribute&lt;ConstructorAtb&gt;</c>); there is no per-entity install behaviour (exactly like
    /// <see cref="Pulsar4X.Stations.StationChassisAtb"/> and the weapon atbs).
    ///
    /// <see cref="ConstructionCapacity"/> is the meaningful dial (CONVENTIONS §16 — the benefit AND the cost are
    /// apparent): the max total module VOLUME (m³) the constructor can assemble in one build, so a bigger constructor
    /// (which costs more to build) can raise a bigger station. Double-arg ctor for the JSON/NCalc binder (landmine L7);
    /// the ctor-arg order must match the template's <c>AtbConstrArgs(...)</c> order.
    /// </summary>
    public class ConstructorAtb : IComponentDesignAttribute
    {
        /// <summary>The largest buildable this constructor can assemble on site, measured as the total VOLUME (m³) of
        /// the recipe's components (chassis + modules). A recipe bigger than this is refused (the constructor isn't
        /// rated for it). 0 = can build nothing (a deliberately crippled design).</summary>
        [JsonProperty] public double ConstructionCapacity { get; internal set; }

        public ConstructorAtb() { }

        public ConstructorAtb(double constructionCapacity)
        {
            ConstructionCapacity = constructionCapacity < 0 ? 0 : constructionCapacity;
        }

        // Read by the on-site construction order at build time — inert on install/uninstall.
        public void OnComponentInstallation(Entity parentEntity, ComponentInstance componentInstance) { }
        public void OnComponentUninstallation(Entity parentEntity, ComponentInstance componentInstance) { }

        public string AtbName() => "Field Constructor";
        public string AtbDescription() =>
            $"A field constructor — assembles a designed buildable on site from carried components, up to {ConstructionCapacity:0} m³ of parts.";
    }
}
