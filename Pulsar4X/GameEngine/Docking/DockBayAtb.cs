using Newtonsoft.Json;
using Pulsar4X.Components;
using Pulsar4X.Datablobs;
using Pulsar4X.Engine;
using Pulsar4X.Interfaces;

namespace Pulsar4X.Docking
{
    /// <summary>
    /// A DOCK — berths for whole vessels, and the one cargo class the compartment system could not express.
    ///
    /// <para><b>Why it is its OWN area and not another cargo hold</b> (the developer's call, 2026-07-30). Every other
    /// compartment measures its contents in <b>cubic metres of stuff poured in</b>. A dock does not: it holds a
    /// <b>discrete vessel that arrives and leaves under its own power</b>, and the question it answers is
    /// <i>"how many, and how big?"</i> — not <i>"how much fits?"</i>. Pouring a frigate into a warehouse by volume
    /// would let you carry half a frigate.</para>
    ///
    /// <para><b>Two gates, and they are the whole decision</b> — deliberately the same shape as
    /// <c>GroundUnitAssembly</c>'s carry budget, because it is the same problem:
    /// <list type="bullet">
    /// <item><see cref="BerthTonnage"/> — total docked hull mass the bay supports (the budget).</item>
    /// <item><see cref="MaxHullMass"/> — the largest SINGLE vessel that physically fits through the door (the per-item
    /// cap). Without it, twenty fighters and one cruiser of the same total mass would be the same design problem, and
    /// they are not.</item>
    /// </list>
    /// So a carrier is <b>many small berths</b> and a tender is <b>one big one</b>, out of two numbers.</para>
    ///
    /// <para><b>Currency is MASS</b>, matching the Chassis door's ship budget (<c>ShipDesign.MassBudget</c>) — so a
    /// dock's capacity and the hull it is bolted to are denominated in the same thing, and a bay big enough to swallow
    /// a cruiser costs a hull that could not have carried it anyway.</para>
    ///
    /// <para><b>Cradle to grave:</b> researched → designed at the <c>docking-bay</c> template → built from materials →
    /// installed on a hull or a station → <see cref="DockTools"/> gates what may dock → <b>shot off, and everything
    /// aboard is set loose</b> (<see cref="DockTools.UndockAll"/> is the grave rung).</para>
    ///
    /// <para>An <see cref="IComponentDesignAttribute"/> so it rides the normal research/design/build/install/save rails
    /// (<c>CONVENTIONS §6</c>). Capacity is summed ON DEMAND by <see cref="DockTools"/> — the
    /// <c>GroundBayAtb</c>/fortification pattern — so the install/uninstall hooks are deliberately no-ops.</para>
    /// </summary>
    public class DockBayAtb : BaseDataBlob, IComponentDesignAttribute
    {
        /// <summary>Total docked hull mass this bay supports, in kg. The budget.</summary>
        [JsonProperty] public double BerthTonnage { get; internal set; }

        /// <summary>The largest single vessel that fits, in kg. The per-item cap — a carrier is not a bigger tender.</summary>
        [JsonProperty] public double MaxHullMass { get; internal set; }

        public DockBayAtb() { }

        // double args: the JSON binder feeds AtbConstrArgs(PropertyValue(...)) values as doubles through NCalc, so the
        // ctor must take doubles or the base-mod component silently fails to bind (landmine L7 / gotcha #10).
        public DockBayAtb(double berthTonnage, double maxHullMass)
        {
            BerthTonnage = berthTonnage < 0 ? 0 : berthTonnage;
            // A bay whose door is bigger than its whole capacity is meaningless; clamp rather than trust the data.
            MaxHullMass = maxHullMass < 0 ? 0 : (maxHullMass > BerthTonnage ? BerthTonnage : maxHullMass);
        }

        public DockBayAtb(DockBayAtb other)
        {
            BerthTonnage = other.BerthTonnage;
            MaxHullMass = other.MaxHullMass;
        }

        // L12: a DataBlob that forgets Clone() silently becomes a bare System.Object when its entity is moved between
        // managers (a ship jumping systems). BaseDataBlob.Clone() is virtual with a garbage default, so this is not optional.
        public override object Clone() => new DockBayAtb(this);

        public void OnComponentInstallation(Entity parentEntity, ComponentInstance componentInstance) { }
        public void OnComponentUninstallation(Entity parentEntity, ComponentInstance componentInstance) { }

        public string AtbName() => "Docking Bay";

        public string AtbDescription() =>
            $"Berths for whole vessels: {BerthTonnage:N0} kg of docked hull, largest single vessel {MaxHullMass:N0} kg. "
            + "Docked ships travel with the carrier and cannot manoeuvre until released.";
    }
}
