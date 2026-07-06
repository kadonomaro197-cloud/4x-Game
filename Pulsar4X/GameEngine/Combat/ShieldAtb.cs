using Newtonsoft.Json;
using Pulsar4X.Engine;
using Pulsar4X.Interfaces;
using Pulsar4X.Components;

namespace Pulsar4X.Combat
{
    /// <summary>
    /// A SHIELD GENERATOR — the space "shield" mechanism on the defence axis (docs/UNIVERSAL-ASSEMBLY-DESIGN.md §2b),
    /// the space twin of the ground <c>GroundDamageMatrix</c> shield (developer's call 2026-07-06: space wants a real
    /// shield layer). A depleting + regenerating energy POOL that absorbs incoming fire BEFORE the hull's toughness,
    /// with the weapon-NATURE matchup (soaks Kinetic, Energy bleeds through, Exotic anti-shield bypasses). Read by
    /// <see cref="ShipCombatValueDB"/> into the ship's shield pool; the resolve depletes/regens it (a later slice —
    /// "shields at 40%!"). A component (CONVENTIONS §6): researched → built → installed → LOST (a shot-off generator
    /// drops the ship's shield — the grave rung). **Additive:** a ship with no shield generator has a 0 pool, so combat
    /// is byte-identical until a shield is actually fitted. Design: docs/WEAPON-TAXONOMY-DESIGN.md §6.
    /// </summary>
    public class ShieldAtb : IComponentDesignAttribute
    {
        /// <summary>The shield POOL size in joules (same currency as toughness) — how much fire it soaks before the
        /// hull starts taking hits.</summary>
        [JsonProperty] public double Capacity_J { get; internal set; }

        /// <summary>How fast the pool refills, joules/sec — the "shields recharging" rate between salvos.</summary>
        [JsonProperty] public double RegenRate_Jps { get; internal set; }

        public ShieldAtb() { }

        // double args for the JSON/NCalc binder (landmine L7). Order = template PropertyFormula order.
        public ShieldAtb(double capacity_J, double regenRate_Jps)
        {
            Capacity_J = capacity_J < 0 ? 0 : capacity_J;
            RegenRate_Jps = regenRate_Jps < 0 ? 0 : regenRate_Jps;
        }

        // Read by ShipCombatValueDB, not an install hook — inert on install/uninstall (like the weapon atbs).
        public void OnComponentInstallation(Entity parentEntity, ComponentInstance componentInstance) { }
        public void OnComponentUninstallation(Entity parentEntity, ComponentInstance componentInstance) { }

        public string AtbName() => "Shield Generator";
        public string AtbDescription() => $"A shield generator — a {Capacity_J:0} J pool, recharging {RegenRate_Jps:0} J/s.";
    }
}
