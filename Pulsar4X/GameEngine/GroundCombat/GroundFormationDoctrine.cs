using System;
using Pulsar4X.Blueprints;

namespace Pulsar4X.GroundCombat
{
    /// <summary>
    /// Read/set a formation's active STANCE — the ground echo of <c>Combat.FleetDoctrine</c>. The catalog of
    /// selectable stances is <c>ModDataStore.GroundStances</c> (loaded from groundStances.json). The ground resolver
    /// (<c>GroundForcesProcessor.ResolveRegionCombat</c>) reads <see cref="AttackMult"/> / <see cref="DamageTakenMult"/>
    /// per unit (via its formation) as read-time multipliers — never baked into a unit's stats, so switching is
    /// reversible. Units in no formation read the neutral 1.0.
    /// </summary>
    public static class GroundFormationDoctrine
    {
        /// <summary>The formation a unit belongs to, or null (unformed / not found).</summary>
        private static GroundFormation FormationOf(GroundForcesDB forces, GroundUnit unit)
        {
            if (forces == null || unit == null || unit.FormationId < 0) return null;
            foreach (var f in forces.Formations)
                if (f.FormationId == unit.FormationId) return f;
            return null;
        }

        /// <summary>The ATTACK multiplier a unit fights at, from its formation's stance (1.0 if unformed / no stance).</summary>
        public static double AttackMult(GroundForcesDB forces, GroundUnit unit)
        {
            var f = FormationOf(forces, unit);
            return (f != null && f.AttackMult > 0) ? f.AttackMult : 1.0;
        }

        /// <summary>The DAMAGE-TAKEN multiplier a unit suffers, from its formation's stance (1.0 if unformed / no stance).</summary>
        public static double DamageTakenMult(GroundForcesDB forces, GroundUnit unit)
        {
            var f = FormationOf(forces, unit);
            return (f != null && f.DamageTakenMult > 0) ? f.DamageTakenMult : 1.0;
        }

        /// <summary>
        /// Set a formation's stance from a catalog blueprint, honouring the switch cooldown (the ground echo of
        /// <c>FleetDoctrine.TrySetDoctrine</c>). Returns false (no change) if still within the cooldown window.
        /// <paramref name="now"/> is the current game time.
        /// </summary>
        public static bool TrySetStance(GroundFormation formation, GroundStanceBlueprint stance, DateTime now)
        {
            if (formation == null || stance == null) return false;
            if (now < formation.SwitchableAfter) return false;   // still on cooldown

            formation.StanceId = stance.UniqueID;
            formation.StanceFamily = stance.Family;
            formation.AttackMult = stance.AttackMult;
            formation.DamageTakenMult = stance.DamageTakenMult;
            formation.SwitchableAfter = now + TimeSpan.FromSeconds(stance.CooldownSeconds);
            return true;
        }

        /// <summary>
        /// Set a formation's RULES OF ENGAGEMENT — its maneuver intent (Hold / Close / Stand-off), the ground echo of
        /// <c>FleetDoctrine.SetEngagementPosture</c>. A direct call (like the space posture), so it works mid-battle
        /// and is separate from the combat-mult stance above. Instant (no cooldown — an intent, not a costed switch).
        /// </summary>
        public static void SetEngagementStance(GroundFormation formation, GroundEngagementStance stance)
        {
            if (formation == null) return;
            formation.Engagement = stance;
        }

        /// <summary>A unit's formation ROE (HoldGround if unformed / no formation) — read by the maneuver step.</summary>
        public static GroundEngagementStance EngagementOf(GroundForcesDB forces, GroundUnit unit)
        {
            var f = FormationOf(forces, unit);
            return f != null ? f.Engagement : GroundEngagementStance.HoldGround;
        }
    }
}
