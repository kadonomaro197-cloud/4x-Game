using Newtonsoft.Json;
using Pulsar4X.Interfaces;
using Pulsar4X.Components;
using Pulsar4X.Engine;
using Pulsar4X.Datablobs;

namespace Pulsar4X.GroundCombat
{
    /// <summary>
    /// GROUND-DEFENCE gear on a buildable installation — the component that makes a building a FORTIFICATION
    /// (a bunker / bastion / fortress). It's what turns "depending on base design" into reality: a building fortifies
    /// only if it carries this attribute, so a Bunker hardens its region while a solar panel does nothing. The
    /// cradle-to-grave counter for ground combat's fortification: researched → built → installed in a region → it
    /// raises the defender's protection → destroyed = the region softens.
    ///
    /// Two values, both in the fortification currency (added to the defender's bonus, capped at
    /// <see cref="GroundFortification.Cap"/>):
    ///   • <see cref="LocalFortify"/> — how much it fortifies the region it stands in.
    ///   • <see cref="AdjacentProjection"/> — how much it projects to each ADJACENT region the same faction holds
    ///     (a fortress shields its neighbours). Deliberately smaller than the local value.
    ///
    /// A component attribute (implements <see cref="IComponentDesignAttribute"/>) so it rides the normal
    /// research/design/build/install/save rails — the reason <c>CONVENTIONS.md</c> §6 says abilities are components.
    /// The fortification is summed ON DEMAND by <see cref="GroundFortification"/> (like population support), so the
    /// install/uninstall hooks are no-ops.
    /// </summary>
    public class GroundDefenseAtb : BaseDataBlob, IComponentDesignAttribute
    {
        /// <summary>Fortification added to THIS region (e.g. 0.25 = +25% defender protection).</summary>
        [JsonProperty] public double LocalFortify { get; internal set; }
        /// <summary>Fortification projected to each ADJACENT same-faction region (e.g. 0.12 = +12%).</summary>
        [JsonProperty] public double AdjacentProjection { get; internal set; }

        public GroundDefenseAtb() { }
        public GroundDefenseAtb(double localFortify, double adjacentProjection)
        {
            LocalFortify = localFortify;
            AdjacentProjection = adjacentProjection;
        }

        public override object Clone() => new GroundDefenseAtb(LocalFortify, AdjacentProjection);

        public void OnComponentInstallation(Entity parentEntity, ComponentInstance componentInstance) { }
        public void OnComponentUninstallation(Entity parentEntity, ComponentInstance componentInstance) { }

        public string AtbName() => "Ground Defence";
        public string AtbDescription()
            => $"Fortifies its region (+{LocalFortify:P0}) and projects to adjacent friendly regions (+{AdjacentProjection:P0}).";
    }
}
