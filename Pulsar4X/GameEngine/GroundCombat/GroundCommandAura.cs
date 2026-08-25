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

        // ── STEADINESS (E14 Rally/Dread — the SHELVED effects, un-shelved) ──────────────────────────────────────────
        // Rally/Dread act on a unit's combat MORALE, not its firepower/toughness (those are Command/Ward). There is no
        // separate rout state machine: steadiness scales the tactical brain's PERCEIVED odds, so a rallied battalion
        // HOLDS at worse odds (retreats later / commits bolder) and a dreaded one BREAKS sooner — folded into
        // GroundTactics.DecidePosture via GroundTacticsContext.Steadiness. Both seats read the same value; the AI ACTS on
        // it (the retreat threshold), the player SEES it (the GroundUnit.Steadiness readout) and retreats by hand.

        /// <summary>Floor on steadiness — a fully-dreaded battalion is shaky but never a zero-strength paper tiger.</summary>
        public const double MinSteadiness = 0.25;
        /// <summary>Ceiling on steadiness — rally can hearten a force but can't manufacture an army out of a squad.</summary>
        public const double MaxSteadiness = 2.0;

        /// <summary>
        /// The aura-derived STEADINESS multiplier for <paramref name="factionId"/>'s units on <paramref name="body"/>:
        /// <c>1 + (best friendly RALLY) − (best enemy DREAD)</c>, clamped to [<see cref="MinSteadiness"/>,
        /// <see cref="MaxSteadiness"/>]. A friendly <see cref="AuraEffect.Rally"/> building (Target Friends/Everyone)
        /// lifts it; an ENEMY <see cref="AuraEffect.Dread"/> building (Target Foes/Everyone — a field meant to land on
        /// the enemy, i.e. on US) drops it. Strongest of each wins (never the sum — the <c>MultFor</c> guard-rail).
        /// Returns 1.0 (neutral) when the gate is off, the body is null, or no matching building exists → byte-identical.
        /// Defensive; never throws. Read by <c>GroundTacticalBrain.BuildContext</c> into the retreat/commit decision.
        /// </summary>
        public static double SteadinessMultFor(Entity body, int factionId)
        {
            if (!EnableGroundCommandAura || body == null)
                return 1.0;

            double rally = 0.0;   // best FRIENDLY Rally (heartens us)
            double dread = 0.0;   // best ENEMY Dread (shakes us)
            foreach (var comps in GroundBuildings.BodyComponentStores(body))
            {
                if (comps?.OwningEntity == null)
                    continue;
                bool mine = comps.OwningEntity.FactionOwnerID == factionId;
                if (!comps.TryGetComponentsByAttribute<AuraAtb>(out var list))
                    continue;
                foreach (var inst in list)
                {
                    if (inst?.Design == null || !inst.Design.TryGetAttribute<AuraAtb>(out AuraAtb atb))
                        continue;
                    if (mine)
                    {
                        // My own Rally field — landing on my side (Friends or Everyone; a Foes-only field wouldn't buff me).
                        if (atb.Effect == AuraEffect.Rally && atb.Target != AuraTarget.Foes && atb.Magnitude > rally)
                            rally = atb.Magnitude;
                    }
                    else
                    {
                        // An enemy's Dread field aimed AT us (Foes or Everyone; a Friends-only field lands on THEIR side).
                        if (atb.Effect == AuraEffect.Dread && atb.Target != AuraTarget.Friends && atb.Magnitude > dread)
                            dread = atb.Magnitude;
                    }
                }
            }

            double steadiness = 1.0 + rally - dread;
            if (steadiness < MinSteadiness) steadiness = MinSteadiness;
            if (steadiness > MaxSteadiness) steadiness = MaxSteadiness;
            return steadiness;
        }
    }
}
