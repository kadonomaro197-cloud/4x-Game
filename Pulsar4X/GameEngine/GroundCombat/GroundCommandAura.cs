using Pulsar4X.Combat;      // AuraAtb, AuraEffect, AuraTarget
using Pulsar4X.Engine;      // Entity

namespace Pulsar4X.GroundCombat
{
    /// <summary>
    /// The GROUND half of the fleet-wide aura command buff (E14 Fork B, slice 3b — the developer's call: a
    /// "flagship/fleet-wide command buff which also applies to planetary combat"). Mirrors
    /// <c>CombatEngagement.FleetAuraMult</c>: a faction's best aura BUILDING on the body buffs ALL its battalions
    /// there — Command→firepower, Ward→toughness, the strongest projector wins (never the sum).
    ///
    /// <para><b>Why a building, not a unit-carried component.</b> Space sources the buff from an <see cref="AuraAtb"/>
    /// projector mounted on a ship in the fleet; a ground unit can't carry one (it's a data object with no component
    /// store). So — exactly as <c>GroundFortification</c> reads <c>GroundDefenseAtb</c> off the body's colony/outpost
    /// stores (<c>GroundBuildings.BodyComponentStores</c>) — the ground aura is a command BUILDING, scanned off
    /// <c>ComponentInstancesDB</c> via <c>TryGetComponentsByAttribute&lt;AuraAtb&gt;</c>. A destroyed building drops
    /// out of the store, so the grave rung comes for free.</para>
    ///
    /// <para><b>Byte-identical:</b> <see cref="EnableGroundCommandAura"/> defaults OFF → <see cref="MultFor"/> returns
    /// 1.0 (the firepower ×1.0 and toughness ÷1.0 folds are exact no-ops), and no base-mod aura building exists yet.
    /// The building template (which also unblocks the space projector) is a later content slice.</para>
    /// </summary>
    public static class GroundCommandAura
    {
        /// <summary>Master gate — OFF leaves both folds exact no-ops so planetary combat is byte-identical until an
        /// aura building is buildable and the developer flips it on.</summary>
        public static bool EnableGroundCommandAura = false;

        /// <summary>
        /// <c>1 + </c> the strongest matching aura magnitude among <paramref name="factionId"/>'s buildings on
        /// <paramref name="body"/> — <see cref="AuraEffect.Command"/> for firepower, <see cref="AuraEffect.Ward"/> for
        /// toughness. Returns 1.0 when the gate is off, the body is null, or the faction has no matching building. A
        /// <see cref="AuraTarget.Foes"/> field does not buff its own side. Defensive; never throws.
        /// </summary>
        public static double MultFor(Entity body, int factionId, AuraEffect want)
        {
            if (!EnableGroundCommandAura || body == null)
                return 1.0;

            double best = 0.0;
            foreach (var comps in GroundBuildings.BodyComponentStores(body))
            {
                if (comps?.OwningEntity == null || comps.OwningEntity.FactionOwnerID != factionId)
                    continue;
                if (!comps.TryGetComponentsByAttribute<AuraAtb>(out var list))
                    continue;
                foreach (var inst in list)
                {
                    if (inst?.Design == null || !inst.Design.TryGetAttribute<AuraAtb>(out AuraAtb atb))
                        continue;
                    if (atb.Effect != want || atb.Target == AuraTarget.Foes)
                        continue;
                    if (atb.Magnitude > best)
                        best = atb.Magnitude;
                }
            }
            return 1.0 + best;
        }
    }
}
